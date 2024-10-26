<?php

namespace App\PostsQuery;

use App\DTO\Post\SortablePost;
use App\DTO\PostKey\Reply as ReplyKey;
use App\DTO\PostKey\SubReply as SubReplyKey;
use App\DTO\PostKey\Thread as ThreadKey;
use App\Entity\Post\Content\ReplyContent;
use App\Entity\Post\Content\SubReplyContent;
use App\DTO\Post\Reply;
use App\DTO\Post\SubReply;
use App\DTO\Post\Thread;
use App\Helper;
use App\Repository\Post\PostRepositoryFactory;
use Illuminate\Support\Collection;
use Symfony\Component\Stopwatch\Stopwatch;

/** @psalm-import-type PostsKeyByTypePluralName from CursorCodec */
readonly class PostsTree
{
    /** @var Collection<int, Thread> */
    public Collection $threads;

    /** @var Collection<int, Reply> */
    public Collection $replies;

    /** @var Collection<int, SubReply> */
    public Collection $subReplies;

    public function __construct(
        private Stopwatch $stopwatch,
        private PostRepositoryFactory $postRepositoryFactory,
    ) {}

    /**
     * @return array{
     *     matchQueryPostCount: array{thread?: int, reply?: int, subReply?: int},
     *     notMatchQueryParentPostCount: array{thread: int, reply: int},
     * }
     */
    public function fillWithParentPost(QueryResult $result): array
    {
        /** @var Collection<int> $tids */
        $tids = $result->threads->map(fn(ThreadKey $postKey) => $postKey->postId);
        /** @var Collection<int> $pids */
        $pids = $result->replies->map(fn(ReplyKey $postKey) => $postKey->postId);
        /** @var Collection<int> $spids */
        $spids = $result->subReplies->map(fn(SubReplyKey $postKey) => $postKey->postId);
        $postModels = $this->postRepositoryFactory->newForumPosts($result->fid);

        $this->stopwatch->start('fillWithThreadsFields');
        /** @var Collection<int, int> $parentThreadsID parent tid of all replies and their sub replies */
        $parentThreadsID = $result->replies
            ->map(fn(ReplyKey $postKey) => $postKey->parentPostId)
            ->concat($result->subReplies->map(fn(SubReplyKey $postKey) => $postKey->tid))
            ->unique();
        $this->threads = collect($postModels['thread']->getPosts($parentThreadsID->concat($tids)))
            ->map(fn(\App\Entity\Post\Thread $entity) => Thread::fromEntity($entity))
            ->each(static fn(Thread $thread) =>
                $thread->setIsMatchQuery($tids->contains($thread->getTid())));
        $this->stopwatch->stop('fillWithThreadsFields');

        $this->stopwatch->start('fillWithRepliesFields');
        /** @var Collection<int, int> $parentRepliesID parent pid of all sub replies */
        $parentRepliesID = $result->subReplies->map(fn(SubReplyKey $postKey) => $postKey->parentPostId)->unique();
        $allRepliesId = $parentRepliesID->concat($pids);
        $this->replies = collect($postModels['reply']->getPosts($allRepliesId))
            ->map(fn(\App\Entity\Post\Reply $entity) => Reply::fromEntity($entity))
            ->each(static fn(Reply $reply) =>
                $reply->setIsMatchQuery($pids->contains($reply->getPid())));
        $this->stopwatch->stop('fillWithRepliesFields');

        $this->stopwatch->start('fillWithSubRepliesFields');
        $this->subReplies = collect($postModels['subReply']->getPosts($spids))
            ->map(fn(\App\Entity\Post\SubReply $entity) => SubReply::fromEntity($entity));
        $this->stopwatch->stop('fillWithSubRepliesFields');

        $this->stopwatch->start('parsePostContentProtoBufBytes');
        // not using one-to-one association due to relying on PostRepository->getTableNameSuffix()
        $replyContents = collect($this->postRepositoryFactory
            ->newReplyContent($result->fid)->getPostsContent($allRepliesId))
                ->mapWithKeys(fn(ReplyContent $content) => [$content->getPid() => $content->getContent()]);
        $this->replies->each(fn(Reply $reply) =>
            $reply->setContent($replyContents->get($reply->getPid())));

        $subReplyContents = collect($this->postRepositoryFactory
            ->newSubReplyContent($result->fid)->getPostsContent($spids))
                ->mapWithKeys(fn(SubReplyContent $content) => [$content->getSpid() => $content->getContent()]);
        $this->subReplies->each(fn(SubReply $subReply) =>
            $subReply->setContent($subReplyContents->get($subReply->getSpid())));
        $this->stopwatch->stop('parsePostContentProtoBufBytes');

        return [
            'matchQueryPostCount' => collect(Helper::POST_TYPES)
                ->combine([$tids, $pids, $spids])
                ->map(static fn(Collection $ids, string $type) => $ids->count())
                ->toArray(),
            'notMatchQueryParentPostCount' => [
                'thread' => $parentThreadsID->diff($tids)->count(),
                'reply' => $parentRepliesID->diff($pids)->count(),
            ],
        ];
    }

    /** @return Collection<int, Thread> */
    public function nestPostsWithParent(): Collection
    {
        $replies = $this->replies->groupBy(fn(Reply $reply) => $reply->getTid());
        $subReplies = $this->subReplies->groupBy(fn(SubReply $subReply) => $subReply->getPid());
        return $this->threads->map(fn(Thread $thread) =>
            $thread->setReplies(
                $replies
                    ->get($thread->getTid(), collect())
                    ->map(fn(Reply $reply) =>
                        $reply->setSubReplies($subReplies->get($reply->getPid(), collect()))),
            ));
    }

    /**
     * @param Collection<int, Thread> $nestedPosts
     * @return Collection<int, Thread>
     */
    public function reOrderNestedPosts(
        Collection $nestedPosts,
        string $orderByField,
        bool $orderByDesc,
    ): Collection {
        $sortBySortingKey = static fn(Collection $posts): Collection => $posts
            ->sortBy(fn(SortablePost $post) => $post->getSortingKey(), descending: $orderByDesc)
            ->values(); // reset keys
        $getOrderByProp = 'get' . ucfirst($orderByField);
        return $sortBySortingKey($nestedPosts->map(
            function (Thread $thread) use ($getOrderByProp, $orderByField, $orderByDesc, $sortBySortingKey): Thread {
                $thread->setReplies($sortBySortingKey($thread->getReplies()->map(
                    function (Reply $reply) use ($getOrderByProp, $orderByField, $orderByDesc): Reply {
                        $reply->setSubReplies($reply->getSubReplies()->sortBy(
                            fn(SubReply $subReplies) => $subReplies->{$getOrderByProp}(),
                            descending: $orderByDesc,
                        )->values()); // reset keys
                        return $this->setSortingKeyForSortablePost($reply, $reply->getSubReplies(), $getOrderByProp, $orderByDesc);
                    },
                )));
                $this->setSortingKeyForSortablePost($thread, $thread->getReplies(), $getOrderByProp, $orderByDesc);
                return $thread;
            },
        ));
    }

    /**
     * @template T of Thread|Reply
     * @param T $currentPost
     * @param Collection<(T is Thread ? Reply : (T is Reply ? SubReply : never))> $subPosts
     * @return T
     */
    private function setSortingKeyForSortablePost(
        SortablePost $currentPost,
        Collection $subPosts,
        string $getOrderByProp,
        bool $orderByDesc,
    ): SortablePost {
        // use the topmost value between sorting key or value of orderBy field within its sub-posts
        /* @var ?(T is Thread ? Reply : (T is Reply ? SubReply : never)) $firstSubPost */
        $firstSubPost = $subPosts->first();
        $currentAndSubPostSortingKeys = collect([
            // value of orderBy field in the first sorted sub-post that isMatchQuery after previous sorting
            $subPosts // sub replies won't have isMatchQuery
                ->filter(static fn(SortablePost $p) => $p->getIsMatchQuery() === true)
                // if no sub-posts matching the query, use null as the sorting key
                ->first()
                ?->{$getOrderByProp}(),
            // sorting key from the first sorted sub-posts
            // not requiring isMatchQuery since a sub-post without isMatchQuery
            // might have its own sub-posts with isMatchQuery
            // and its sortingKey would be selected from its own sub-posts
            $firstSubPost?->getSortingKey(),
        ]);
        if ($currentPost->getIsMatchQuery() === true) {
            // also try to use the value of orderBy field in the current post
            $currentAndSubPostSortingKeys->push($currentPost->{$getOrderByProp}());
        }

        // Collection->filter() will remove falsy values like null
        $currentAndSubPostSortingKeys = $currentAndSubPostSortingKeys->filter()->sort();
        $currentPost->setSortingKey($orderByDesc
            ? $currentAndSubPostSortingKeys->last()
            : $currentAndSubPostSortingKeys->first());

        return $currentPost;
    }
}
