<?php

namespace App\Http\Controllers;

use App\Helper;
use App\Http\PostsQuery\IndexQuery;
use App\Http\PostsQuery\SearchQuery;
use App\Http\PostsQuery\ParamsValidator;
use App\Tieba\Eloquent\ForumModel;
use App\Tieba\Eloquent\UserModel;
use GuzzleHttp\Utils;
use Illuminate\Support\Arr;

class PostsQuery extends Controller
{
    private int $perPageItems = 200;

    public function query(\Illuminate\Http\Request $request): array
    {
        $validator = new ParamsValidator((array)Utils::jsonDecode(
            $request->validate([
                'page' => 'integer',
                'query' => 'json'
            ])['query'],
            true
        ));
        $params = $validator->params;

        $isQueryByPostID =
            // is there no other params except post ID params
            \count($params->omit(...Helper::POST_ID)) === 0
            // is all post ID params doesn't own any sub param
            && array_filter($params->pick(...Helper::POST_ID), static fn ($p) => $p->getAllSub() !== []) === [];
        $isFidParamNull = $params->getUniqueParamValue('fid') === null;
        // is the fid param exists and there's no other params
        $isQueryByFid = !$isFidParamNull && $params->count() === \count($params->pick(...ParamsValidator::UNIQUE_PARAMS_NAME));
        $isIndexQuery = $isQueryByPostID || $isQueryByFid;
        $isSearchQuery = !$isIndexQuery;
        if ($isSearchQuery) {
            Helper::abortAPIIf(40002, $isFidParamNull);
        }

        $validator->addDefaultParamsThenValidate($isIndexQuery);

        $queryClass = $isIndexQuery ? IndexQuery::class : SearchQuery::class;
        $query = (new $queryClass($this->perPageItems))->query($params);
        $result = $query->fillWithParentPost();

        return [
            'type' => $isIndexQuery ? 'index' : 'search',
            'pages' => [
                ...$query->getResultPages(),
                ...Arr::only($result, ['parentThreadCount', 'parentReplyCount'])
            ],
            'forum' => ForumModel::fid($result['fid'])->hidePrivateFields()->first()?->toArray(),
            'threads' => $query::nestPostsWithParent(...$result),
            'users' => UserModel::whereIn(
                'uid',
                $result['threads']
                    ->pluck('latestReplierUid')
                    ->concat(array_map(
                        static fn (string $type) => $result[$type]->pluck('authorUid'),
                        Helper::POST_TYPES_PLURAL
                    ))
                    ->flatten()->unique()
                    ->sort()->toArray()
            )->hidePrivateFields()->get()->toArray()
        ];
    }
}
