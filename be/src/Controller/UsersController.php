<?php

namespace App\Controller;

use App\Helper;
use App\PostsQuery\QueryResult;
use App\Repository\UserRepository;
use App\Validator\Validator;
use Doctrine\ORM\QueryBuilder;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Component\Validator\Constraints as Assert;

class UsersController extends AbstractController
{
    private int $perPageItems = 200;

    public function __construct(
        private readonly Validator $validator,
        private readonly UserRepository $userRepository,
    ) {}

    #[Route('/api/users')]
    public function query(Request $request): array
    {
        $queryParams = $request->query->all();
        $paramConstraints = [
            'uid' => new Assert\Type('digit'),
            'name' => new Assert\Type('string'),
            'displayName' => new Assert\Type('string'),
            'gender' => new Assert\Choice(['0', '1', '2', 'NULL']),
        ];
        $this->validator->validate($queryParams, new Assert\Collection($paramConstraints, allowMissingFields: true));

        $queries = collect($queryParams)
            ->reduce(
                function (array $acc, $paramValue, string $paramName) use ($paramConstraints): array {
                    /** @var array{int, QueryBuilder} $acc */
                    [$paramIndex, $queryBuilder] = $acc;
                    if (!array_key_exists($paramName, $paramConstraints)) {
                        throw new \InvalidArgumentException();
                    }
                    return [$paramIndex + 1, $paramValue === 'NULL'
                        && in_array($paramName, ['name', 'displayName', 'gender'], true)
                        ? $queryBuilder->andWhere("t.$paramName IS NULL")
                        : $queryBuilder->andWhere("t.$paramName = ?$paramIndex")
                            ->setParameter($paramIndex, $paramValue)];
                },
                [0, $this->userRepository->createQueryBuilder('t')],
            )[1]->orderBy('t.uid', 'DESC');

        ['result' => $result, 'hasMorePages' => $hasMorePages] =
            QueryResult::hasMorePages($queries, $this->perPageItems);
        $resultCount = count($result);
        Helper::abortAPIIf(40402, $resultCount === 0);

        return [
            'pages' => [
                'itemCount' => $resultCount,
                'hasMore' => $hasMorePages,
            ],
            'users' => $result,
        ];
    }
}
