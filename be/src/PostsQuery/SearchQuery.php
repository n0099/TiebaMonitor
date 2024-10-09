<?php

namespace App\PostsQuery;

use App\Repository\Post\PostRepository;
use App\Repository\Post\PostRepositoryFactory;
use App\Repository\UserRepository;
use Doctrine\ORM\QueryBuilder;
use Illuminate\Support\Collection;
use Symfony\Component\Serializer\Normalizer\NormalizerInterface;
use Symfony\Component\Stopwatch\Stopwatch;

class SearchQuery extends BaseQuery
{
    public function __construct(
        NormalizerInterface $normalizer,
        Stopwatch $stopwatch,
        CursorCodec $cursorCodec,
        private readonly PostRepositoryFactory $postRepositoryFactory,
        private readonly UserRepository $userRepository,
    ) {
        parent::__construct($normalizer, $stopwatch, $cursorCodec, $postRepositoryFactory);
    }

    public function query(QueryParams $params, ?string $cursor): void
    {
        /** @var int $fid */
        $fid = $params->getUniqueParamValue('fid');
        /** @var array<string, array> $cachedUserQueryResult key by param name */
        $cachedUserQueryResult = [];
        /** @var Collection<string, QueryBuilder> $queries key by post type */
        $queries = collect($this->postRepositoryFactory->newForumPosts($fid))
            ->only($params->getUniqueParamValue('postTypes'))
            ->map(function (PostRepository $repository) use ($params, &$cachedUserQueryResult): QueryBuilder {
                $postQuery = $repository->selectCurrentAndParentPostID();
                foreach ($params->omit() as $paramIndex => $param) { // omit nothing to get all params
                    // even when $cachedUserQueryResult[$param->name] is null
                    // it will still pass as a reference to the array item
                    // that is null at this point, but will be later updated by ref
                    $postQuery = self::applyQueryParamsOnQuery(
                        $postQuery,
                        $param,
                        $paramIndex,
                        $cachedUserQueryResult[$param->name],
                    );
                }
                return $postQuery;
            });

        $orderByParam = $params->pick('orderBy')[0];
        $this->orderByField = $orderByParam->value;
        $this->orderByDesc = $orderByParam->getSub('direction');
        if ($this->orderByField === 'default') {
            $this->orderByField = 'postedAt';
            $this->orderByDesc = true;
        }

        $this->setResult($fid, $queries, $cursor);
    }

    /**
     * Apply conditions of query params on a query builder that created from posts query builder
     */
    private function applyQueryParamsOnQuery(
        QueryBuilder $query,
        QueryParam $param,
        int $paramIndex,
        ?array &$outCachedUserQueryResult,
    ): QueryBuilder {
        $sqlParamName = "param$paramIndex";
        $name = $param->name;
        $value = $param->value;
        $sub = $param->getAllSub();
        $sub['not'] ??= false;
        $not = $sub['not'] ? 'NOT' : '';
        $notBoolStr = $sub['not'] ? 'true' : 'false';

        $fieldNameOfNumericParams = [
            'threadViewCount' => 'viewCount',
            'threadShareCount' => 'shareCount',
            'threadReplyCount' => 'replyCount',
            'replySubReplyCount' => 'subReplyCount',
        ][$name] ?? $name;
        $inverseRanges = [
            '<' => '>=',
            '=' => '!=',
            '>' => '<=',
        ];
        if (array_key_exists('range', $sub) && !array_key_exists($sub['range'], $inverseRanges)) {
            throw new \InvalidArgumentException();
        }
        $inverseRangeOfNumericParams = $inverseRanges[$sub['range'] ?? null] ?? null;

        $userTypeOfUserParams = str_starts_with($name, 'author') ? 'author' : 'latestReplier';
        $fieldNameOfUserNameParams = str_ends_with($name, 'DisplayName') ? 'displayName' : 'name';
        $getAndCacheUserQuery =
            static function (QueryBuilder $newQueryWhenCacheMiss) use (&$outCachedUserQueryResult): Collection {
                // $outCachedUserQueryResult === null means it's the first call
                $outCachedUserQueryResult ??= $newQueryWhenCacheMiss->getQuery()->getResult();
                return $outCachedUserQueryResult;
            };

        $whereBetween = static function (string $field) use ($query, $not, $value, $sqlParamName) {
            $values = explode(',', $value);
            return $query->andWhere("t.$field $not BETWEEN :{$sqlParamName}_0 AND :{$sqlParamName}_1")
                ->setParameter("{$sqlParamName}_0", $values[0])
                ->setParameter("{$sqlParamName}_1", $values[1]);
        };
        return match ($name) {
            // numeric
            'tid', 'pid', 'spid',
            'authorUid', 'authorExpGrade', 'latestReplierUid',
            'threadViewCount', 'threadShareCount', 'threadReplyCount', 'replySubReplyCount' =>
                // phpcs:disable Generic.WhiteSpace.ScopeIndent
                match ($sub['range']) {
                    'IN' => $query->andWhere("t.$fieldNameOfNumericParams $not IN (:$sqlParamName)")
                        ->setParameter($sqlParamName, explode(',', $value)),
                    'BETWEEN' => $whereBetween($fieldNameOfNumericParams),
                    default => $query->andWhere(
                        "t.$fieldNameOfNumericParams "
                            . ($sub['not'] ? $inverseRangeOfNumericParams : $sub['range'])
                            . " :$sqlParamName",
                    )->setParameter($sqlParamName, $value),
                },
            // textMatch
            'threadTitle', 'postContent' =>
                self::applyTextMatchParamOnQuery(
                    $query,
                    $name === 'threadTitle' ? 'title' : 'content',
                    $value,
                    $sub,
                    $sqlParamName,
                ),
            // dateTimeRange
            'postedAt', 'latestReplyPostedAt' => $whereBetween($name),
            // array
            'threadProperties' => static function () use ($not, $notBoolStr, $value, $query) {
                foreach ($value as $threadProperty) {
                    match ($threadProperty) {
                        'good' => $query->andWhere("t.isGood = $notBoolStr"),
                        'sticky' => $query->andWhere("t.stickyType IS $not NULL"),
                    };
                }
                return $query;
            },
            'authorName', 'latestReplierName', 'authorDisplayName', 'latestReplierDisplayName' =>
                $query->andWhere("t.{$userTypeOfUserParams}Uid $not IN (:$sqlParamName)")
                    ->setParameter(
                        $sqlParamName,
                        $getAndCacheUserQuery(self::applyTextMatchParamOnQuery(
                            $this->userRepository->createQueryBuilder('t')->select('t.uid'),
                            $fieldNameOfUserNameParams,
                            $value,
                            $sub,
                            $sqlParamName,
                        )),
                    ),
            'authorGender', 'latestReplierGender' =>
                $query->andWhere("t.{$userTypeOfUserParams}Uid $not IN (:$sqlParamName)")
                    ->setParameter(
                        $sqlParamName,
                        $getAndCacheUserQuery($this->userRepository->createQueryBuilder('t')
                            ->select('t.uid')
                            ->andWhere("t.gender = :{$sqlParamName}_gender")
                            ->setParameter("{$sqlParamName}_gender", $value)),
                    ),
            'authorManagerType' =>
                $value === 'NULL'
                    ? $query->andWhere("t.authorManagerType IS $not NULL")
                    : $query->andWhere('t.authorManagerType ' . ($sub['not'] ? '!=' : '=') . " :$sqlParamName")
                        ->setParameter($sqlParamName, $value),
            default => $query,
        };
    }

    /** @psalm-param array<string, mixed> $subParams */
    private static function applyTextMatchParamOnQuery(
        QueryBuilder $query,
        string $field,
        string $value,
        array $subParams,
        string $sqlParamName,
    ): QueryBuilder {
        $not = $subParams['not'] === true ? 'NOT' : '';
        if ($subParams['matchBy'] === 'regex') {
            return $query->andWhere("t.$field $not REGEXP :$sqlParamName")->setParameter($sqlParamName, $value);
        }

        // split multiple search keyword by space char when $subParams['spaceSplit'] == true
        foreach ($subParams['spaceSplit'] ? explode(' ', $value) : [$value] as $keywordIndex => $keyword) {
            if ($not === 'NOT') {
                $query = $query->andWhere("t.$field NOT LIKE :{$sqlParamName}_$keywordIndex");
            } else { // not (A or B) <=> not A and not B, following https://en.wikipedia.org/wiki/De_Morgan%27s_laws
                $query = $query->orWhere("t.$field LIKE :{$sqlParamName}_$keywordIndex");
            }
            $query = $query->setParameter(
                "{$sqlParamName}_$keywordIndex",
                $subParams['matchBy'] === 'implicit' ? "%$keyword%" : $keyword,
            );
        }
        return $query;
    }
}