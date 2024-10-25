<?php

namespace App\Controller;

use App\DTO\User\AuthorExpGrade;
use App\DTO\User\ForumModerator;
use App\DTO\User\User;
use App\Entity\Post\Post;
use App\Entity\Post\Thread;
use App\Helper;
use App\PostsQuery\BaseQuery;
use App\PostsQuery\IndexQuery;
use App\PostsQuery\ParamsValidator;
use App\PostsQuery\SearchQuery;
use App\Repository\ForumRepository;
use App\Repository\LatestReplierRepository;
use App\Repository\Revision\AuthorExpGradeRepository;
use App\Repository\Revision\ForumModeratorRepository;
use App\Repository\UserRepository;
use App\Validator\Validator;
use Illuminate\Support\Arr;
use Illuminate\Support\Collection;
use Psr\Container\ContainerInterface;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\DependencyInjection\Attribute\AutowireLocator;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Component\Stopwatch\Stopwatch;
use Symfony\Component\Validator\Constraints as Assert;

class PostsController extends AbstractController
{
    public function __construct(
        private readonly Stopwatch $stopwatch,
        private readonly Validator $validator,
        private readonly ForumRepository $forumRepository,
        private readonly UserRepository $userRepository,
        private readonly LatestReplierRepository $latestReplierRepository,
        private readonly ForumModeratorRepository $forumModeratorRepository,
        private readonly AuthorExpGradeRepository $authorExpGradeRepository,
        #[AutowireLocator([
            ParamsValidator::class,
            IndexQuery::class,
            SearchQuery::class,
        ])]
        private readonly ContainerInterface $locator,
    ) {}

    #[Route('/api/posts')]
    public function query(Request $request): array
    {
        $this->validator->validate($request->query->all(), new Assert\Collection([
            'cursor' => new Assert\Optional(new Assert\Regex(
                // https://stackoverflow.com/questions/475074/regex-to-parse-or-validate-base64-data
                // (,|$)|,){5,6} means allow at most 5~6 parts of base64 segment or empty string to exist
                '/^(([A-Za-z0-9-_]{4})*([A-Za-z0-9-_]{2,3})(,|$)|,){5,6}$/',
            )),
            'query' => new Assert\Required(new Assert\Json()),
        ]));
        $validator = $this->locator->get(ParamsValidator::class);
        $params = $validator->setParams(\Safe\json_decode($request->query->get('query'), true))->getParams();

        $postIDParams = $params->pick(...Helper::POST_ID);
        $isQueryByPostID =
            // is there no other params except unique params and post ID params
            \count($params->omit(...ParamsValidator::UNIQUE_PARAMS_NAME, ...Helper::POST_ID)) === 0
            // is there only one post ID param
            && \count($postIDParams) === 1
            // is all post ID params doesn't own any sub param
            && array_filter($postIDParams, static fn($p) => $p->getAllSub() !== []) === [];
        $isFidParamNull = $params->getUniqueParamValue('fid') === null;
        // is the fid param exists and there's no other params except unique params
        $isQueryByFid = !$isFidParamNull && \count($params->omit(...ParamsValidator::UNIQUE_PARAMS_NAME)) === 0;
        $isIndexQuery = $isQueryByPostID || $isQueryByFid;
        $isSearchQuery = !$isIndexQuery;
        Helper::abortAPIIf(40002, $isSearchQuery && $isFidParamNull);

        $validator->addDefaultParamsThenValidate(shouldSkip40003: $isIndexQuery);

        $this->stopwatch->start('$queryClass->query()');
        /** @var BaseQuery $query */
        $query = $this->locator->get($isIndexQuery ? IndexQuery::class : SearchQuery::class);
        $query->query($params, $request->query->get('cursor'));
        $this->stopwatch->stop('$queryClass->query()');
        $this->stopwatch->start('fillWithParentPost');
        $matchQueryPostCounts = $query->postsTree->fillWithParentPost($query->queryResult);
        $this->stopwatch->stop('fillWithParentPost');

        $this->stopwatch->start('queryUsers');
        $latestRepliers = $this->latestReplierRepository->getLatestRepliersWithoutNameWhenHasUid(
            $query->postsTree->threads->map(fn(Thread $thread) => $thread->getLatestReplierId()),
        );
        $uids = collect([$query->postsTree->threads, $query->postsTree->replies, $query->postsTree->subReplies])
            ->flatMap(static fn(Collection $posts) => $posts->map(fn(Post $post) => $post->getAuthorUid()))
            ->concat($latestRepliers->pluck('uid')->filter()) // filter() will remove NULLs
            ->unique();
        $users = collect($this->userRepository->getUsers($uids))
            ->map(fn(\App\Entity\User $entity) => User::fromEntity($entity));
        $this->stopwatch->stop('queryUsers');

        $this->stopwatch->start('queryUserRelated');
        $fid = $query->queryResult->fid;
        $authorExpGrades = collect($this->authorExpGradeRepository->getLatestOfUsers($fid, $uids))
            ->keyBy(fn(AuthorExpGrade $authorExpGrade) => $authorExpGrade->uid);
        $users->each(fn(User $user) => $user->setCurrentAuthorExpGrade($authorExpGrades[$user->getUid()]));

        $forumModerators = collect($this->forumModeratorRepository
                ->getLatestOfUsers($fid, $users->map(fn(User $user) => $user->getPortrait())))
            ->keyBy(fn(ForumModerator $forumModerator) => $forumModerator->portrait);
        $users->each(fn(User $user) => $user->setCurrentForumModerator($forumModerators->get($user->getPortrait())));
        $this->stopwatch->stop('queryUserRelated');

        return [
            'type' => $isIndexQuery ? 'index' : 'search',
            'pages' => [
                'currentCursor' => $query->queryResult->currentCursor,
                'nextCursor' => $query->queryResult->nextCursor,
                ...$matchQueryPostCounts,
            ],
            'forum' => $this->forumRepository->getForum($fid),
            'threads' => $query->postsTree->reOrderNestedPosts(
                $query->postsTree->nestPostsWithParent(),
                $query->orderByField,
                $query->orderByDesc,
            ),
            'users' => $users,
            'latestRepliers' => $latestRepliers,
        ];
    }
}
