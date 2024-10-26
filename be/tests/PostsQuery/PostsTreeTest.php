<?php

namespace App\Tests\PostsQuery;

use App\DTO\Post\Reply;
use App\DTO\Post\SubReply;
use App\DTO\Post\Thread;
use App\PostsQuery\PostsTree;
use Illuminate\Support\Collection;
use PHPUnit\Framework\Attributes\CoversClass;
use PHPUnit\Framework\Attributes\DataProvider;
use Symfony\Bundle\FrameworkBundle\Test\KernelTestCase;

#[CoversClass(PostsTree::class)]
class PostsTreeTest extends KernelTestCase
{
    private PostsTree $sut;

    protected function setUp(): void
    {
        parent::setUp();
        self::bootKernel();
        $container = self::getContainer();
        $this->sut = $container->get(PostsTree::class);
    }

    #[DataProvider('provideReOrderNestedPostsData')]
    public function testReOrderNestedPosts(Collection $input, bool $orderByDesc, Collection $expected): void
    {
        self::assertEquals($expected, $this->sut->reOrderNestedPosts($input, 'postedAt', $orderByDesc));
    }

    public static function provideReOrderNestedPostsData(): array
    {
        $input = collect([
            (new Thread())
                ->setPostedAt(1)
                ->setIsMatchQuery(true)
                ->setReplies(collect([
                    (new Reply())
                        ->setPostedAt(2)
                        ->setIsMatchQuery(true)
                        ->setSubReplies(collect([
                            (new SubReply())->setPostedAt(30),
                        ])),
                    (new Reply())
                        ->setPostedAt(20)
                        ->setIsMatchQuery(false)
                        ->setSubReplies(collect([
                            (new SubReply())->setPostedAt(3),
                        ])),
                    (new Reply())
                        ->setPostedAt(4)
                        ->setIsMatchQuery(false)
                        ->setSubReplies(collect([
                            (new SubReply())->setPostedAt(5),
                            (new SubReply())->setPostedAt(33)->setIsMatchQuery(false),
                            (new SubReply())->setPostedAt(60),
                        ])),
                ])),
            (new Thread())
                ->setPostedAt(7)
                ->setIsMatchQuery(false)
                ->setReplies(collect([
                    (new Reply())
                        ->setPostedAt(31)
                        ->setIsMatchQuery(true)
                        ->setSubReplies(collect()),
                ])),
        ]);
        $expectedWhenOrderByAsc = collect([
            (new Thread())
                ->setPostedAt(1)
                ->setIsMatchQuery(true)
                ->setReplies(collect([
                    (new Reply())
                        ->setPostedAt(2)
                        ->setIsMatchQuery(true)
                        ->setSubReplies(collect([
                            (new SubReply())->setPostedAt(30),
                        ]))
                        ->setSortingKey(2),
                    (new Reply())
                        ->setPostedAt(20)
                        ->setIsMatchQuery(false)
                        ->setSubReplies(collect([
                            (new SubReply())->setPostedAt(3),
                        ]))
                        ->setSortingKey(3),
                    (new Reply())
                        ->setPostedAt(4)
                        ->setIsMatchQuery(false)
                        ->setSubReplies(collect([
                            (new SubReply())->setPostedAt(5),
                            (new SubReply())->setPostedAt(33)->setIsMatchQuery(false),
                            (new SubReply())->setPostedAt(60),
                        ]))
                        ->setSortingKey(5),
                ]))
                ->setSortingKey(1),
            (new Thread())
                ->setPostedAt(7)
                ->setIsMatchQuery(false)
                ->setReplies(collect([
                    (new Reply())
                        ->setPostedAt(31)
                        ->setIsMatchQuery(true)
                        ->setSubReplies(collect())
                        ->setSortingKey(31),
                ]))
                ->setSortingKey(31),
        ]);
        $expectedWhenOrderByDesc = collect([
            (new Thread())
                ->setPostedAt(1)
                ->setIsMatchQuery(true)
                ->setReplies(collect([
                    (new Reply())
                        ->setPostedAt(4)
                        ->setIsMatchQuery(false)
                        ->setSubReplies(collect([
                            (new SubReply())->setPostedAt(60),
                            (new SubReply())->setPostedAt(33)->setIsMatchQuery(false),
                            (new SubReply())->setPostedAt(5),
                        ]))
                        ->setSortingKey(60),
                    (new Reply())
                        ->setPostedAt(2)
                        ->setIsMatchQuery(true)
                        ->setSubReplies(collect([
                            (new SubReply())->setPostedAt(30),
                        ]))
                        ->setSortingKey(30),
                    (new Reply())
                        ->setPostedAt(20)
                        ->setIsMatchQuery(false)
                        ->setSubReplies(collect([
                            (new SubReply())->setPostedAt(3),
                        ]))
                        ->setSortingKey(3),
                ]))
                ->setSortingKey(60),
            (new Thread())
                ->setPostedAt(7)
                ->setIsMatchQuery(false)
                ->setReplies(collect([
                    (new Reply())
                        ->setPostedAt(31)
                        ->setIsMatchQuery(true)
                        ->setSubReplies(collect())
                        ->setSortingKey(31),
                ]))
                ->setSortingKey(31),
        ]);
        return [
            [$input, false, $expectedWhenOrderByAsc],
            [$input, true, $expectedWhenOrderByDesc],
        ];
    }

    /** @param array{threads: Collection<Thread>, replies: Collection<Reply>, subReplies: Collection<SubReply>} $input */
    #[DataProvider('provideNestPostsWithParent')]
    public function testNestPostsWithParent(array $input, Collection $expected): void
    {
        (new \ReflectionProperty(PostsTree::class, 'threads'))->setValue($this->sut, $input['threads']);
        (new \ReflectionProperty(PostsTree::class, 'replies'))->setValue($this->sut, $input['replies']);
        (new \ReflectionProperty(PostsTree::class, 'subReplies'))->setValue($this->sut, $input['subReplies']);
        self::assertEquals($expected, $this->sut->nestPostsWithParent());
    }

    public static function provideNestPostsWithParent(): array
    {
        return [[
            [
                'threads' => collect([(new Thread())->setTid(1)]),
                'replies' => collect([(new Reply())->setTid(1)->setPid(2)]),
                'subReplies' => collect([(new SubReply())->setTid(1)->setPid(2)->setSpid(3)]),
            ],
            collect([(new Thread())
                ->setTid(1)
                ->setReplies(collect([
                    (new Reply())
                        ->setTid(1)->setPid(2)
                        ->setSubReplies(collect([
                            (new SubReply())->setTid(1)->setPid(2)->setSpid(3),
                        ])),
                ])),
            ]),
        ]];
    }
}
