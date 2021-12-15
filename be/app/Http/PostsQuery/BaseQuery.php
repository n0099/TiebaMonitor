<?php

namespace App\Http\PostsQuery;

use App\Helper;
use App\Tieba\Eloquent\PostModelFactory;
use App\Tieba\Post\Post;
use Illuminate\Support\Collection;

trait BaseQuery
{
    protected array $queryResult;

    protected array $queryResultPages;

    abstract public function query(QueryParams $queryParams): self;

    public function __construct(protected int $perPageItems)
    {
    }

    public function getResult(): array
    {
        return $this->queryResult;
    }

    public function getResultPages(): array
    {
        return $this->queryResultPages;
    }

    public function fillWithParentPost(): array
    {
        $queryResult = $this->queryResult;
        $isInfoOnlyContainsPostsID = $this instanceof IndexQuery;
        $postModels = PostModelFactory::getPostModelsByFid($queryResult['fid']);
        $tids = array_column($queryResult['threads'], 'tid');
        $pids = array_column($queryResult['replies'], 'pid');
        $spids = array_column($queryResult['subReplies'], 'spid');

        $queryDetailedPostsInfo = static function ($postIDs, $postType) use ($postModels, $isInfoOnlyContainsPostsID) {
            if ($postIDs === []) {
                return collect();
            }
            $model = $postModels[$postType];
            return collect($isInfoOnlyContainsPostsID
                ? $model->{Helper::POSTS_TYPE_ID[$postType]}($postIDs)->hidePrivateFields()->get()->toArray()
                : $model);
        };
        $threads = $queryDetailedPostsInfo($tids, 'thread');
        $replies = $queryDetailedPostsInfo($pids, 'reply');
        $subReplies = $queryDetailedPostsInfo($spids, 'subReply');

        $isSubIDsMissInOriginIDs = static fn (Collection $originIDs, Collection $subIDs): bool
            => $subIDs->contains(static fn (int $subID): bool => !$originIDs->contains($subID));

        $tidsInReplies = $replies->pluck('tid')
            ->concat($subReplies->pluck('tid'))->unique()->sort()->values();
        // $tids must be first argument to ensure the existence of diffed $tidsInReplies
        if ($isSubIDsMissInOriginIDs(collect($tids), $tidsInReplies)) {
            // fetch complete threads info which appeared in replies and sub replies info but missing in $tids
            $threads = collect($postModels['thread']
                ->tid($tidsInReplies->concat($tids)->toArray())
                ->hidePrivateFields()->get()->toArray());
        }

        $pidsInThreadsAndSubReplies = $subReplies->pluck('pid')
            // append thread's first reply when there's no pid
            ->concat($pids === [] ? $threads->pluck('firstPid') : [])
            ->unique()->sort()->values();
        // $pids must be first argument to ensure the diffed $pidsInSubReplies existing
        if ($isSubIDsMissInOriginIDs(collect($pids), $pidsInThreadsAndSubReplies)) {
            // fetch complete replies info which appeared in threads and sub replies info but missing in $pids
            $replies = collect($postModels['reply']
                ->pid($pidsInThreadsAndSubReplies->concat($pids)->toArray())
                ->hidePrivateFields()->get()->toArray());
        }

        $convertJsonContentToHtml = static function (array $post) {
            if ($post['content'] !== null) {
                $post['content'] = Post::convertJsonContentToHtml($post['content']);
            }
            return $post;
        };
        $replies->transform($convertJsonContentToHtml);
        $subReplies->transform($convertJsonContentToHtml);

        return array_merge(
            ['fid' => $queryResult['fid']],
            array_combine(Helper::POST_TYPES_PLURAL, [$threads->toArray(), $replies->toArray(), $subReplies->toArray()])
        );
    }

    public static function nestPostsWithParent(array $threads, array $replies, array $subReplies, int $fid): array
    {
        // adding useless parameter $fid will compatible with array shape of field $this->queryResult when passing it as spread arguments
        $threads = Helper::keyBy($threads, 'tid');
        $replies = Helper::keyBy($replies, 'pid');
        $subReplies = Helper::keyBy($subReplies, 'spid');
        $nestedPostsInfo = [];

        foreach ($threads as $tid => $thread) {
            // can't invoke values() here to prevent losing key with posts id
            $threadReplies = collect($replies)->where('tid', $tid)->toArray();
            foreach ($threadReplies as $pid => $reply) {
                // values() and array_values() remove keys to simplify json data
                $threadReplies[$pid]['subReplies'] = collect($subReplies)->where('pid', $pid)->values()->toArray();
            }
            $nestedPostsInfo[$tid] = array_merge($thread, ['replies' => array_values($threadReplies)]);
        }

        return array_values($nestedPostsInfo);
    }
}
