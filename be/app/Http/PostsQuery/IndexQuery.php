<?php

namespace App\Http\PostsQuery;

use App\Helper;
use App\Tieba\Eloquent\IndexModel;
use Illuminate\Support\Arr;

class IndexQuery
{
    use BaseQuery;

    public function query(QueryParams $queryParams): self
    {
        $flatQueryParams = array_reduce(
            $queryParams->filter(...ParamsValidator::UNIQUE_PARAMS_NAME, ...Helper::POSTS_ID),
            static fn (array $accParams, Param $param) => array_merge($accParams, [$param->name => $param->value]),
            []
        ); // flatten unique query params
        $indexQuery = IndexModel::where(Arr::only($flatQueryParams, ['fid', ...Helper::POSTS_ID]));
        if ($flatQueryParams['orderBy'] !== 'default') {
            $indexQuery->orderBy($flatQueryParams['orderBy'], $flatQueryParams['direction']);
        } elseif (Arr::only($flatQueryParams, Helper::POSTS_ID) === []) { // query by fid only
            $indexQuery->orderByDesc('postTime'); // order by postTime to prevent posts out of order when order by post id
        } else { // query by post id
            // order by all posts id to keep reply and sub reply continuous instated of clip into multi-page since they are varied in postTime
            $indexQuery->orderByMulti(array_fill_keys(Helper::POSTS_ID, 'ASC'));
        }

        $result = $indexQuery->whereIn('type', $flatQueryParams['postTypes'])->simplePaginate($this->perPageItems);
        Helper::abortAPIIf(40401, $result->isEmpty());

        $this->queryResult = array_merge(
            ['fid' => $result->pluck('fid')->first()],
            array_combine(Helper::POST_TYPES_PLURAL, array_map( // assign queried posts id from $indexQuery
                static fn ($postType) => $result->where('type', $postType)->toArray(),
                Helper::POST_TYPES
            ))
        );
        $this->queryResultPages = [
            'firstItem' => $result->firstItem(),
            'itemsCount' => $result->count(),
            'currentPage' => $result->currentPage()
        ];

        return $this;
    }
}
