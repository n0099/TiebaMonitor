<?php

namespace App\PostsQuery;

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
use Symfony\Component\Serializer\Normalizer\NormalizerInterface;
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
        private NormalizerInterface $normalizer,
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
     * @return Collection<int, Collection<string, mixed|Collection<int, Collection<string, mixed|Collection<int, Collection<string, mixed>>>>>>
     * @SuppressWarnings(PHPMD.CamelCaseParameterName)
     */
    public function nestPostsWithParent(): Collection
    {
        $this->stopwatch->start('nestPostsWithParent');

        $replies = $this->replies->groupBy(fn(Reply $reply) => $reply->getTid());
        $subReplies = $this->subReplies->groupBy(fn(SubReply $subReply) => $subReply->getPid());
        $ret = $this->threads
            ->map(fn(Thread $thread) => [
                ...$this->normalizer->normalize($thread),
                'replies' => $replies
                    ->get($thread->getTid(), collect())
                    ->map(fn(Reply $reply) => [
                        ...$this->normalizer->normalize($reply),
                        'subReplies' => $this->normalizer->normalize($subReplies->get($reply->getPid(), collect())),
                    ]),
            ])
            ->recursive();

        $this->stopwatch->stop('nestPostsWithParent');
        return $ret;
    }

    /**
     * @phpcs:ignore Generic.Files.LineLength.TooLong
     * @param Collection<int, Collection<string, mixed|Collection<int, Collection<string, mixed|Collection<int, Collection<string, mixed>>>>>> $nestedPosts
     * @return list<array<string, mixed|list<array<string, mixed|list<array<string, mixed>>>>>>
     */
    public function reOrderNestedPosts(
        Collection $nestedPosts,
        string $orderByField,
        bool $orderByDesc,
        bool $shouldRemoveSortingKey = true,
    ): array {
        $this->stopwatch->start('reOrderNestedPosts');

        /**
         * @param Collection<int, Collection<string, mixed|Collection<int, Collection<string, mixed>>>> $curPost
         * @param string $childPostTypePluralName
         * @return Collection<int, Collection<string, mixed|Collection<int, Collection<string, mixed>>>>
         */
        $setSortingKeyFromCurrentAndChildPosts = static function (
            Collection $curPost,
            string $childPostTypePluralName,
        ) use ($orderByField, $orderByDesc): Collection {
            /** @var Collection<int, Collection<string, mixed>> $childPosts sorted child posts */
            $childPosts = $curPost[$childPostTypePluralName];
            $curPost[$childPostTypePluralName] = $childPosts->values(); // reset keys

            // use the topmost value between sorting key or value of orderBy field within its child posts
            $curAndChildSortingKeys = collect([
                // value of orderBy field in the first sorted child post that isMatchQuery after previous sorting
                $childPosts // sub replies won't have isMatchQuery
                ->filter(static fn(Collection $p) => ($p['isMatchQuery'] ?? true) === true)
                    // if no child posts matching the query, use null as the sorting key
                    ->first()[$orderByField] ?? null,
                // sorting key from the first sorted child posts
                // not requiring isMatchQuery since a child post without isMatchQuery
                // might have its own child posts with isMatchQuery
                // and its sortingKey would be selected from its own child posts
                $childPosts->first()['sortingKey'] ?? null,
            ]);
            if ($curPost['isMatchQuery'] === true) {
                // also try to use the value of orderBy field in current post
                $curAndChildSortingKeys->push($curPost[$orderByField]);
            }

            // Collection->filter() will remove falsy values like null
            $curAndChildSortingKeys = $curAndChildSortingKeys->filter()->sort();
            $curPost['sortingKey'] = $orderByDesc
                ? $curAndChildSortingKeys->last()
                : $curAndChildSortingKeys->first();

            return $curPost;
        };
        $sortBySortingKey = static fn(Collection $posts): Collection => $posts
            ->sortBy(fn(Collection $i) => $i['sortingKey'], descending: $orderByDesc);
        $removeSortingKey = $shouldRemoveSortingKey
            ? /** @psalm-return Collection<array-key, Collection> */
            static fn(Collection $posts): Collection => $posts
                ->map(fn(Collection $i) => $i->except('sortingKey'))
            : static fn($i) => $i;
        $ret = $removeSortingKey($sortBySortingKey(
            $nestedPosts->map(
                /**
                 * @param Collection{replies: Collection} $thread
                 * @return Collection{replies: Collection}
                 */
                function (Collection $thread) use (
                    $orderByField,
                    $orderByDesc,
                    $sortBySortingKey,
                    $removeSortingKey,
                    $setSortingKeyFromCurrentAndChildPosts
                ) {
                    $thread['replies'] = $sortBySortingKey($thread['replies']->map(
                        /**
                         * @param Collection{subReplies: Collection} $reply
                         * @return Collection{subReplies: Collection}
                         */
                        function (Collection $reply) use ($orderByField, $orderByDesc, $setSortingKeyFromCurrentAndChildPosts) {
                            $reply['subReplies'] = $reply['subReplies']->sortBy(
                                fn(Collection $subReplies) => $subReplies->get($orderByField),
                                descending: $orderByDesc,
                            );
                            return $setSortingKeyFromCurrentAndChildPosts($reply, 'subReplies');
                        },
                    ));
                    $setSortingKeyFromCurrentAndChildPosts($thread, 'replies');
                    $thread['replies'] = $removeSortingKey($thread['replies']);
                    return $thread;
                },
            ),
        ))->values()->toArray();

        $this->stopwatch->stop('reOrderNestedPosts');
        return $ret;
    }
}
