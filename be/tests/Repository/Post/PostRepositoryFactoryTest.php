<?php

namespace App\Tests\Repository\Post;

use App\Repository\Post\PostRepositoryFactory;
use App\Repository\RepositoryWithSplitFid;
use PHPUnit\Framework\Attributes\CoversClass;
use PHPUnit\Framework\Attributes\DataProvider;
use Symfony\Bundle\FrameworkBundle\Test\KernelTestCase;

#[CoversClass(PostRepositoryFactory::class)]
#[CoversClass(RepositoryWithSplitFid::class)]
class PostRepositoryFactoryTest extends KernelTestCase
{
    private PostRepositoryFactory $sut;

    protected function setUp(): void
    {
        parent::setUp();
        self::bootKernel();
        $container = static::getContainer();
        $this->sut = $container->get(PostRepositoryFactory::class);
    }

    #[DataProvider('providePostModelFid')]
    public function testPostModelFid(int $fid): void
    {
        self::assertEquals($fid, $this->sut->newThread($fid)->getFid());
        self::assertEquals($fid, $this->sut->newReply($fid)->getFid());
        self::assertEquals($fid, $this->sut->newReplyContent($fid)->getFid());
        self::assertEquals($fid, $this->sut->newSubReply($fid)->getFid());
        self::assertEquals($fid, $this->sut->newSubReplyContent($fid)->getFid());
        self::assertEquals($fid, $this->sut->newForumPosts($fid)['thread']->getFid());
        self::assertEquals($fid, $this->sut->newForumPosts($fid)['reply']->getFid());
        self::assertEquals($fid, $this->sut->newForumPosts($fid)['subReply']->getFid());
        self::assertEquals($fid, $this->sut->new($fid, 'thread')->getFid());
        self::assertEquals($fid, $this->sut->new($fid, 'reply')->getFid());
        self::assertEquals($fid, $this->sut->new($fid, 'subReply')->getFid());
    }

    public static function providePostModelFid(): array
    {
        return [[0]];
    }
}
