<?php

namespace App\Http\PostsQuery;

use App\Helper;
use Illuminate\Support\Arr;
use Illuminate\Validation\Rule;

class ParamsValidator
{
    public const UNIQUE_PARAMS_NAME = ['fid', 'postTypes', 'orderBy'];

    public QueryParams $params;

    protected array $currentPostTypes;

    /**
     * @param array[] $params
     */
    public function __construct(array $params)
    {
        self::validateParamsValue($params);
        $this->params = new QueryParams($params);
        $this->validate40001();
        $this->validate40005();
    }

    public function addDefaultParamsThenValidate(): void
    {
        $this->params->addDefaultValueOnParams();
        $this->params->addDefaultValueOnUniqueParams();
        // sort here to prevent further sort while validating
        $this->params->setUniqueParamValue('postTypes', Arr::sort($this->params->getUniqueParamValue('postTypes')));
        $this->currentPostTypes = (array)$this->params->getUniqueParamValue('postTypes');
        $this->validate40003();
        $this->validate40004();
    }

    protected static function validateParamsValue(array $params): void
    {
        $paramsPossibleValue = [
            'userGender' => [0, 1, 2],
            'userManagerType' => ['NULL', 'manager', 'assist', 'voiceadmin']
        ];
        $dateRangeValidator = static function ($_, string $value) {
            \Validator::make(
                explode(',', $value),
                ['0' => 'date|before_or_equal:1', '1' => 'date|after_or_equal:0']
            )->validate();
        };
        // note here we haven't validated that is every sub param have a corresponding main param yet
        \Validator::make($params, [
            '*.fid' => 'integer',
            '*.postTypes' => 'array|in:thread,reply,subReply',
            '*.orderBy' => 'string|in:postTime,tid,pid,spid',
            '*.direction' => 'in:ASC,DESC',
            '*.tid' => 'integer',
            '*.pid' => 'integer',
            '*.spid' => 'integer',
            '*.postTime' => $dateRangeValidator,
            '*.latestReplyTime' => $dateRangeValidator,
            '*.threadViewNum' => 'integer',
            '*.threadShareNum' => 'integer',
            '*.threadReplyNum' => 'integer',
            '*.replySubReplyNum' => 'integer',
            '*.threadProperties' => 'array|in:good,sticky',
            '*.authorUid' => 'integer',
            '*.authorExpGrade' => 'integer',
            '*.authorGender' => Rule::in($paramsPossibleValue['userGender']),
            '*.authorManagerType' => Rule::in($paramsPossibleValue['userManagerType']),
            '*.latestReplierUid' => 'integer',
            '*.latestReplierGender' => Rule::in($paramsPossibleValue['userGender']),
            // sub param of tid, pid, spid, threadViewNum, threadShareNum, threadReplyNum, replySubReplyNum, authorUid, authorExpGrade, latestReplierUid
            '*.range' => 'in:<,=,>,IN,BETWEEN',
            // sub param of threadTitle, postContent, authorName, authorDisplayName, latestReplierName, latestReplierDisplayName
            '*.matchBy' => 'in:implicit,explicit,regex',
            '*.spaceSplit' => 'boolean'
        ])->validate();
    }

    protected function validate40001(): void
    {
        // only fill postTypes and/or orderBy uniqueParam doesn't query anything
        Helper::abortAPIIf(40001, $this->params->count() === \count($this->params->filter('postTypes', 'orderBy')));
    }

    protected function validate40005(): void
    {
        foreach (self::UNIQUE_PARAMS_NAME as $uniqueParamName) { // is all unique param only appeared once
            Helper::abortAPIIf(40005, \count($this->params->filter($uniqueParamName)) > 1);
        }
    }

    private static function isRequiredPostTypes(array $current, array $required): bool
    {
        $required[1] = Arr::sort($required[1]);
        return $required[0] === 'SUB'
            ? array_diff($current, $required[1]) === []
            : $current ===$required[1];
    }

    protected function validate40003(): void
    {
        $paramsRequiredPostTypes = [
            'pid' => ['SUB', ['reply', 'subReply']],
            'spid' => ['ALL', ['subReply']],
            'latestReplyTime' => ['ALL', ['thread']],
            'threadTitle' => ['ALL', ['thread']],
            'postContent' => ['SUB', ['reply', 'subReply']],
            'threadViewNum' => ['ALL', ['thread']],
            'threadShareNum' => ['ALL', ['thread']],
            'threadReplyNum' => ['ALL', ['thread']],
            'replySubReplyNum' => ['ALL', ['reply']],
            'threadProperties' => ['ALL', ['thread']],
            'authorExpGrade' => ['SUB', ['reply', 'subReply']],
            'latestReplierUid' => ['ALL', ['thread']],
            'latestReplierName' => ['ALL', ['thread']],
            'latestReplierDisplayName' => ['ALL', ['thread']],
            'latestReplierGender' => ['ALL', ['thread']]
        ];
        foreach ($paramsRequiredPostTypes as $paramName => $requiredPostTypes) {
            if ($this->params->filter($paramName) !== []) {
                Helper::abortAPIIfNot(40003, !self::isRequiredPostTypes($this->currentPostTypes, $requiredPostTypes));
            }
        }
    }

    protected function validate40004(): void
    {
        $orderByRequiredPostTypes = [
            'pid' => ['SUB', ['reply', 'subReply']],
            'spid' => ['SUB', ['subReply']]
        ];
        $currentOrderBy = (string)$this->params->getUniqueParamValue('orderBy');
        if (\array_key_exists($currentOrderBy, $orderByRequiredPostTypes)) {
            Helper::abortAPIIfNot(40004, !self::isRequiredPostTypes($this->currentPostTypes, $orderByRequiredPostTypes[$currentOrderBy]));
        }
    }
}