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

    /**
     * @phpcs:ignore Generic.Files.LineLength.TooLong
     * @return Collection<int, Thread>
     * @SuppressWarnings(PHPMD.CamelCaseParameterName)
     */
    public function nestPostsWithParent(): Collection
    {
        $this->stopwatch->start('nestPostsWithParent');

        $replies = $this->replies->groupBy(fn(Reply $reply) => $reply->getTid());
        $subReplies = $this->subReplies->groupBy(fn(SubReply $subReply) => $subReply->getPid());
        $ret = $this->threads->map(fn(Thread $thread) =>
            $thread->setReplies($replies
                ->get($thread->getTid(), collect())
                ->map(fn(Reply $reply) =>
                    $reply->setSubReplies($subReplies->get($reply->getPid(), collect())))
            ));

        $this->stopwatch->stop('nestPostsWithParent');
        return $ret;
    }

    /**
     * @phpcs:ignore Generic.Files.LineLength.TooLong
     * @param Collection<int, Thread> $nestedPosts
     * @return list<array<string, mixed|list<array<string, mixed|list<array<string, mixed>>>>>>
     */
    public function reOrderNestedPosts(
        Collection $nestedPosts,
        string $orderByField,
        bool $orderByDesc,
    ): array {
        $this->stopwatch->start('reOrderNestedPosts');

        /**
         * @template T of Thread|Reply
         * @param T $curPost
         * @param Collection<(T is Thread ? Reply : (T is Reply ? SubReply : never))> $childPosts
         * @return T
         */
        $setSortingKeyFromCurrentAndChildPosts = static function (
            Thread|Reply $curPost,
            Collection $childPosts,
        ) use ($orderByField, $orderByDesc): Thread|Reply {
            // use the topmost value between sorting key or value of orderBy field within its child posts
            /* @var ?(T is Thread ? Reply : (T is Reply ? SubReply : never)) $firstChildPost */
            $firstChildPost = $childPosts->first();
            $curAndChildSortingKeys = collect([
                // value of orderBy field in the first sorted child post that isMatchQuery after previous sorting
                $childPosts // sub replies won't have isMatchQuery
                    ->filter(static fn(SortablePost $p) => $p->getIsMatchQuery() === true)
                    // if no child posts matching the query, use null as the sorting key
                    ->first()
                    ?->{"get$orderByField"}(),
                // sorting key from the first sorted child posts
                // not requiring isMatchQuery since a child post without isMatchQuery
                // might have its own child posts with isMatchQuery
                // and its sortingKey would be selected from its own child posts
                $firstChildPost?->getSortingKey(),
            ]);
            if ($curPost->getIsMatchQuery() === true) {
                // also try to use the value of orderBy field in current post
                $curAndChildSortingKeys->push($curPost->{"get$orderByField"}());
            }

            // Collection->filter() will remove falsy values like null
            $curAndChildSortingKeys = $curAndChildSortingKeys->filter()->sort();
            $curPost->setSortingKey($orderByDesc
                ? $curAndChildSortingKeys->last()
                : $curAndChildSortingKeys->first());

            return $curPost;
        };
        $sortBySortingKey = static fn(Collection $posts): Collection => $posts
            ->sortBy(fn(SortablePost $post) => $post->getSortingKey(), descending: $orderByDesc);
        $ret = $sortBySortingKey(
            $nestedPosts->map(
                function (Thread $thread) use (
                    $orderByField,
                    $orderByDesc,
                    $sortBySortingKey,
                    $setSortingKeyFromCurrentAndChildPosts
                ): Thread {
                    $thread->setReplies($sortBySortingKey($thread->getReplies()->map(
                        function (Reply $reply) use (
                            $orderByField,
                            $orderByDesc,
                            $setSortingKeyFromCurrentAndChildPosts
                        ): Reply {
                            $reply->setSubReplies($reply->getSubReplies()->sortBy(
                                fn(SubReply $subReplies) => $subReplies->{"get$orderByField"}(),
                                descending: $orderByDesc,
                            ));
                            return $setSortingKeyFromCurrentAndChildPosts($reply, $reply->getSubReplies());
                        },
                    )));
                    $setSortingKeyFromCurrentAndChildPosts($thread, $thread->getReplies());
                    return $thread;
                },
            ),
        )->values()->toArray();

        $this->stopwatch->stop('reOrderNestedPosts');
        return $ret;
    }
}
