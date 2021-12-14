<?php

namespace App\Http\PostsQuery;

use App\Helper;
use App\Tieba\Eloquent\IndexModel;
use Illuminate\Support\Arr;

class IndexQuery
{
    use BaseQuery;

    public function toNestedPosts(): array
    {
        return self::getNestedPostsInfoByID($this->queryResult, true);
    }

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
            // order by all posts id to keep reply and sub reply continuous instated of clip into multi page since they are vary in postTime
            $indexQuery = $indexQuery->orderByMulti(array_fill_keys(Helper::POSTS_ID, 'ASC'));
        }

        $indexQuery = $indexQuery->whereIn('type', $flatQueryParams['postTypes'])->simplePaginate($this->perPageItems);
        Helper::abortAPIIf(40401, $indexQuery->isEmpty());

        $postsQueriedInfo = array_merge(
            ['fid' => $indexQuery->pluck('fid')->first()],
            array_map( // assign queried posts id from $indexQuery
                static fn ($postType) => $indexQuery->where('type', $postType)->toArray(),
                array_combine(Helper::POST_TYPES, Helper::POSTS_ID)
            )
        );

        $this->queryResult = $postsQueriedInfo;
        $this->queryResultPages = [
            'firstItem' => $indexQuery->firstItem(),
            'itemsCount' => $indexQuery->count(),
            'currentPage' => $indexQuery->currentPage()
        ];

        return $this;
    }
}