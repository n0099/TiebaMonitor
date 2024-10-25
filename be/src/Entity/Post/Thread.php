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
    #[ORM\Column, ORM\Id] private int $tid;
    #[ORM\Column] private int $threadType;
    #[ORM\Column] private ?string $stickyType;
    #[ORM\Column] private ?string $topicType;
    #[ORM\Column] private ?int $isGood;
    #[ORM\Column] private string $title;
    #[ORM\Column] private int $latestReplyPostedAt;
    #[ORM\Column] private ?int $latestReplierId;
    #[ORM\Column] private ?int $replyCount;
    #[ORM\Column] private ?int $viewCount;
    #[ORM\Column] private ?int $shareCount;
    /** @type ?resource */
    #[ORM\Column] protected $zan;
    /** @type ?resource */
    #[ORM\Column] protected $geolocation;
    #[ORM\Column] private ?string $authorPhoneType;

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

    /** @param resource|null $zan */
    public function setZan(null $zan): void
    {
        $this->zan = $zan;
    }

    public function getGeolocation(): ?array
    {
        return BlobResourceGetter::protoBuf($this->geolocation, Lbs::class);
    }

    /** @param resource|null $geolocation */
    public function setGeolocation(null $geolocation): void
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
