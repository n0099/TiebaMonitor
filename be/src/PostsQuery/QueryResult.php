<?php

namespace App\PostsQuery;

use App\DTO\PostKey\Reply as ReplyKey;
use App\DTO\PostKey\SubReply as SubReplyKey;
use App\DTO\PostKey\Thread as ThreadKey;
use App\Helper;
use Doctrine\ORM\Query\Expr\Comparison;
use Doctrine\ORM\QueryBuilder;
use Illuminate\Support\Collection;
use Symfony\Component\Stopwatch\Stopwatch;

/** @psalm-import-type PostsKeyByTypePluralName from CursorCodec */
readonly class QueryResult
{
    public int $fid;

    /** @var Collection<int, ThreadKey> */
    public Collection $threads;

    /** @var Collection<int, ReplyKey> */
    public Collection $replies;

    /** @var Collection<int, SubReplyKey> */
    public Collection $subReplies;

    public string $currentCursor;

    public ?string $nextCursor;

    public function __construct(
        private Stopwatch $stopwatch,
        private CursorCodec $cursorCodec,
        private int $perPageItems = 50,
    ) {}

    /** @return array{result: Collection, hasMorePages: bool} */
    public static function hasMorePages(QueryBuilder $query, int $limit): array
    {
        $results = collect($query->setMaxResults($limit + 1)->getQuery()->getResult());
        if ($results->count() === $limit + 1) {
            $results->pop();
            $hasMorePages = true;
        }
        return ['result' => $results, 'hasMorePages' => $hasMorePages ?? false];
    }

    /** @param Collection<Helper::POST_TYPE, ?QueryBuilder> $queries */
    public function setResult(
        int $fid,
        Collection $queries,
        ?string $cursorParamValue,
        string $orderByField,
        bool $orderByDesc,
        ?string $queryByPostIDParamName = null,
    ): void {
        $this->stopwatch->start('setResult');

        $this->fid = $fid;
        $cursorsKeyByPostType = null;
        if ($cursorParamValue !== null) {
            $cursorsKeyByPostType = $this->cursorCodec->decodeCursor($cursorParamValue, $orderByField);
            // remove queries for post types with encoded cursor ',,'
            $queries = $queries->intersectByKeys($cursorsKeyByPostType);
        }

        $queries->each(function (QueryBuilder $qb, string $postType) use ($orderByDesc, $orderByField, $cursorsKeyByPostType) {
            $qb->addOrderBy("t.$orderByField", $orderByDesc === true ? 'DESC' : 'ASC')
                // cursor paginator requires values of orderBy column are unique
                // if not it should fall back to other unique field (here is the post ID primary key)
                // https://use-the-index-luke.com/no-offset
                // https://mysql.rjweb.org/doc.php/pagination
                // https://medium.com/swlh/how-to-implement-cursor-pagination-like-a-pro-513140b65f32
                // https://slack.engineering/evolving-api-pagination-at-slack/
                ->addOrderBy('t.' . Helper::POST_TYPE_TO_ID[$postType]);

            $cursors = $cursorsKeyByPostType?->get($postType);
            if ($cursors === null) {
                return;
            }
            $cursors = collect($cursors);
            $comparisons = $cursors->keys()->map(
                fn(string $fieldName): Comparison => $orderByDesc
                    ? $qb->expr()->lt("t.$fieldName", ":cursor_$fieldName")
                    : $qb->expr()->gt("t.$fieldName", ":cursor_$fieldName"),
            );
            $qb->andWhere($qb->expr()->orX(...$comparisons));
            $cursors->mapWithKeys(fn($fieldValue, string $fieldName) =>
            $qb->setParameter("cursor_$fieldName", $fieldValue)); // prevent overwriting existing param
        });

        $resultsAndHasMorePages = $queries->map(fn(QueryBuilder $query) =>
            self::hasMorePages($query, $this->perPageItems));
        /** @var PostsKeyByTypePluralName $postsKeyByTypePluralName */
        $postsKeyByTypePluralName = $resultsAndHasMorePages
            ->mapWithKeys(fn(array $resultAndHasMorePages, string $postType) =>
                [Helper::POST_TYPE_TO_PLURAL[$postType] => $resultAndHasMorePages['result']]);
        Helper::abortAPIIf(40401, $postsKeyByTypePluralName->every(static fn(Collection $i) => $i->isEmpty()));

        $this->threads = $postsKeyByTypePluralName['threads'] ?? collect();
        $this->replies = $postsKeyByTypePluralName['replies'] ?? collect();
        $this->subReplies = $postsKeyByTypePluralName['subReplies'] ?? collect();
        $this->currentCursor = $cursorParamValue ?? '';
        $this->nextCursor = $resultsAndHasMorePages->pluck('hasMorePages')
            ->filter()->isNotEmpty() // filter() remove falsy
            ? $this->cursorCodec->encodeNextCursor(
                $queryByPostIDParamName === null
                    ? $postsKeyByTypePluralName
                    : $postsKeyByTypePluralName->except([Helper::POST_ID_TO_TYPE_PLURAL[$queryByPostIDParamName]]),
            )
            : null;

        $this->stopwatch->stop('setResult');
    }
}
