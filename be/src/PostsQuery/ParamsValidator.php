<?php

namespace App\PostsQuery;

use App\Helper;
use App\Validator\DateTimeRange;
use App\Validator\Validator;
use Illuminate\Support\Arr;
use Symfony\Component\Validator\Constraints as Assert;

class ParamsValidator
{
    public const array UNIQUE_PARAMS_NAME = ['fid', 'postTypes', 'orderBy'];

    private QueryParams $params;

    public function __construct(private readonly Validator $validator) {}

    public function getParams(): QueryParams
    {
        return $this->params;
    }

    /** @param array[] $value */
    public function setParams(array $value): static
    {
        array_map($this->validateParamValue(...), $value);
        $this->params = new QueryParams($value);
        $this->validate40001();
        $this->validate40005();
        return $this;
    }

    public function addDefaultParamsThenValidate(bool $shouldSkip40003): void
    {
        $this->params->addDefaultValueOnParams();
        $this->params->addDefaultValueOnUniqueParams();
        // sort here to prevent further sort while validating
        $sortedPostTypes = collect($this->params->getUniqueParamValue('postTypes'))->sort()->values()->all();
        $this->params->setUniqueParamValue('postTypes', $sortedPostTypes);
        $currentPostTypes = (array) $this->params->getUniqueParamValue('postTypes');
        if (!$shouldSkip40003) {
            $this->validate40003($currentPostTypes);
        }
        $this->validate40004($currentPostTypes);
    }

    private function validateParamValue(array $param): void
    {
        $paramsPossibleValue = [
            'userGender' => ['0', '1', '2'],
            'userManagerType' => ['NULL', 'manager', 'assist', 'voiceadmin'],
        ];
        $numericParams = collect(QueryParams::PARAM_NAME_KEY_BY_TYPE['numeric'])->push('fid')
            ->mapWithKeys(fn(string $paramName) => [$paramName => new Assert\Type(['digit', 'int'])]);
        $textParams = collect(QueryParams::PARAM_NAME_KEY_BY_TYPE['text'])
            ->mapWithKeys(fn(string $paramName) => [$paramName => new Assert\Type('string')]);
        // note here we haven't validated that is every sub param have a corresponding main param yet
        $this->validator->validate($param, new Assert\Collection([
            ...$numericParams,
            ...$textParams,
            'postTypes' => new Assert\All([new Assert\Choice(Helper::POST_TYPES)]),
            'orderBy' => new Assert\Choice([...Helper::POST_ID, 'postedAt']),
            'direction' => new Assert\Choice(['ASC', 'DESC']),
            'postedAt' => new DateTimeRange(),
            'latestReplyPostedAt' => new DateTimeRange(),
            'threadProperties' => new Assert\All([new Assert\Choice(['good', 'sticky'])]),
            'authorGender' => new Assert\Choice($paramsPossibleValue['userGender']),
            'authorManagerType' => new Assert\Choice($paramsPossibleValue['userManagerType']),
            'latestReplierGender' => new Assert\Choice($paramsPossibleValue['userGender']),

            'not' => new Assert\Type('boolean'),
            // sub param of tid, pid, spid
            // threadViewCount, threadShareCount, threadReplyCount, replySubReplyCount
            // authorUid, authorExpGrade, latestReplierUid
            'range' => new Assert\Choice(['<', '=', '>', 'IN', 'BETWEEN']),
            // sub param of threadTitle, postContent
            // authorName, authorDisplayName
            // latestReplierName, latestReplierDisplayName
            'matchBy' => new Assert\Choice(['implicit', 'explicit', 'regex']),
            'spaceSplit' => new Assert\Type('boolean'),
        ], allowMissingFields: true));
    }

    private function validate40001(): void
    {
        // only fill postTypes and/or orderBy uniqueParam doesn't query anything
        Helper::abortAPIIf(40001, $this->params->count() === \count($this->params->pick('postTypes', 'orderBy')));
    }

    private function validate40005(): void
    {
        foreach (self::UNIQUE_PARAMS_NAME as $uniqueParamName) { // is all unique param only appeared once
            Helper::abortAPIIf(40005, \count($this->params->pick($uniqueParamName)) > 1);
        }
    }

    private static function isRequiredPostTypes(array $current, array $required): bool
    {
        /** @var 'SUB' | 'All' $coverage */
        /** @var array $postTypes */
        [$coverage, $postTypes] = $required;
        $postTypes = Arr::sort($postTypes);
        return match ($coverage) {
            'SUB' => array_diff($current, $postTypes) === [],
            'ALL' => $current === $postTypes,
            default => throw new \Exception(),
        };
    }

    public const array REQUIRED_POST_TYPES_KEY_BY_PARAM_NAME = [
        'pid' => ['SUB', ['reply', 'subReply']],
        'spid' => ['ALL', ['subReply']],
        'latestReplyPostedAt' => ['ALL', ['thread']],
        'threadTitle' => ['ALL', ['thread']],
        'postContent' => ['SUB', ['reply', 'subReply']],
        'threadViewCount' => ['ALL', ['thread']],
        'threadShareCount' => ['ALL', ['thread']],
        'threadReplyCount' => ['ALL', ['thread']],
        'replySubReplyCount' => ['ALL', ['reply']],
        'threadProperties' => ['ALL', ['thread']],
        'authorExpGrade' => ['SUB', ['reply', 'subReply']],
        'latestReplierUid' => ['ALL', ['thread']],
        'latestReplierName' => ['ALL', ['thread']],
        'latestReplierDisplayName' => ['ALL', ['thread']],
        'latestReplierGender' => ['ALL', ['thread']],
    ];

    private function validate40003(array $currentPostTypes): void
    {
        foreach (self::REQUIRED_POST_TYPES_KEY_BY_PARAM_NAME as $paramName => $requiredPostTypes) {
            if ($this->params->pick($paramName) !== []) {
                Helper::abortAPIIfNot(40003, self::isRequiredPostTypes($currentPostTypes, $requiredPostTypes));
            }
        }
    }

    public const array REQUIRED_POST_TYPES_KEY_BY_ORDER_BY_VALUE = [
        'pid' => ['SUB', ['reply', 'subReply']],
        'spid' => ['SUB', ['subReply']],
    ];

    private function validate40004(array $currentPostTypes): void
    {
        $currentOrderBy = (string) $this->params->getUniqueParamValue('orderBy');
        if (\array_key_exists($currentOrderBy, self::REQUIRED_POST_TYPES_KEY_BY_ORDER_BY_VALUE)) {
            Helper::abortAPIIfNot(
                40004,
                self::isRequiredPostTypes(
                    $currentPostTypes,
                    self::REQUIRED_POST_TYPES_KEY_BY_ORDER_BY_VALUE[$currentOrderBy],
                ),
            );
        }
    }
}
