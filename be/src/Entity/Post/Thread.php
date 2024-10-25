<?php

namespace App\Entity\Post;

use App\Entity\BlobResourceGetter;
use App\Repository\Post\ThreadRepository;
use Doctrine\ORM\Mapping as ORM;
use TbClient\Post\Common\Lbs;
use TbClient\Post\Common\Zan;

#[ORM\Entity(repositoryClass: ThreadRepository::class)]
class Thread extends Post
{
    #[ORM\Column, ORM\Id] protected int $tid;
    #[ORM\Column] protected int $threadType;
    #[ORM\Column] protected ?string $stickyType;
    #[ORM\Column] protected ?string $topicType;
    #[ORM\Column] protected ?int $isGood;
    #[ORM\Column] protected string $title;
    #[ORM\Column] protected int $latestReplyPostedAt;
    #[ORM\Column] protected ?int $latestReplierId;
    #[ORM\Column] protected ?int $replyCount;
    #[ORM\Column] protected ?int $viewCount;
    #[ORM\Column] protected ?int $shareCount;
    /** @var ?resource */
    #[ORM\Column] protected $zan;
    /** @var ?resource */
    #[ORM\Column] protected $geolocation;
    #[ORM\Column] protected ?string $authorPhoneType;

    public function getTid(): int
    {
        return $this->tid;
    }

    public function setTid(int $tid): void
    {
        $this->tid = $tid;
    }

    public function getThreadType(): int
    {
        return $this->threadType;
    }

    public function setThreadType(int $threadType): void
    {
        $this->threadType = $threadType;
    }

    public function getStickyType(): ?string
    {
        return $this->stickyType;
    }

    public function setStickyType(?string $stickyType): void
    {
        $this->stickyType = $stickyType;
    }

    public function getTopicType(): ?string
    {
        return $this->topicType;
    }

    public function setTopicType(?string $topicType): void
    {
        $this->topicType = $topicType;
    }

    public function getIsGood(): ?int
    {
        return $this->isGood;
    }

    public function setIsGood(?int $isGood): void
    {
        $this->isGood = $isGood;
    }

    public function getTitle(): string
    {
        return $this->title;
    }

    public function setTitle(string $title): void
    {
        $this->title = $title;
    }

    public function getLatestReplyPostedAt(): int
    {
        return $this->latestReplyPostedAt;
    }

    public function setLatestReplyPostedAt(int $latestReplyPostedAt): void
    {
        $this->latestReplyPostedAt = $latestReplyPostedAt;
    }

    public function getLatestReplierId(): ?int
    {
        return $this->latestReplierId;
    }

    public function setLatestReplierId(?int $latestReplierId): void
    {
        $this->latestReplierId = $latestReplierId;
    }

    public function getReplyCount(): int
    {
        return $this->replyCount ?? 0;
    }

    public function setReplyCount(?int $replyCount): void
    {
        $this->replyCount = $replyCount;
    }

    public function getViewCount(): int
    {
        return $this->viewCount ?? 0;
    }

    public function setViewCount(?int $viewCount): void
    {
        $this->viewCount = $viewCount;
    }

    public function getShareCount(): int
    {
        return $this->shareCount ?? 0;
    }

    public function setShareCount(?int $shareCount): void
    {
        $this->shareCount = $shareCount;
    }

    public function getZan(): ?array
    {
        return BlobResourceGetter::protoBuf($this->zan, Zan::class);
    }

    /** @param ?resource $zan */
    public function setZan($zan): void
    {
        $this->zan = $zan;
    }

    public function getGeolocation(): ?array
    {
        return BlobResourceGetter::protoBuf($this->geolocation, Lbs::class);
    }

    /** @param ?resource $geolocation */
    public function setGeolocation($geolocation): void
    {
        $this->geolocation = $geolocation;
    }

    public function getAuthorPhoneType(): ?string
    {
        return $this->authorPhoneType;
    }

    public function setAuthorPhoneType(?string $authorPhoneType): void
    {
        $this->authorPhoneType = $authorPhoneType;
    }
}
