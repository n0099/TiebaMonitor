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

    public function setTid(int $value): self
    {
        $this->tid = $value;
        return $this;
    }

    public function getThreadType(): int
    {
        return $this->threadType;
    }

    public function setThreadType(int $value): self
    {
        $this->threadType = $value;
        return $this;
    }

    public function getStickyType(): ?string
    {
        return $this->stickyType;
    }

    public function setStickyType(?string $value): self
    {
        $this->stickyType = $value;
        return $this;
    }

    public function getTopicType(): ?string
    {
        return $this->topicType;
    }

    public function setTopicType(?string $value): self
    {
        $this->topicType = $value;
        return $this;
    }

    public function getIsGood(): ?int
    {
        return $this->isGood;
    }

    public function setIsGood(?int $value): self
    {
        $this->isGood = $value;
        return $this;
    }

    public function getTitle(): string
    {
        return $this->title;
    }

    public function setTitle(string $value): self
    {
        $this->title = $value;
        return $this;
    }

    public function getLatestReplyPostedAt(): int
    {
        return $this->latestReplyPostedAt;
    }

    public function setLatestReplyPostedAt(int $value): self
    {
        $this->latestReplyPostedAt = $value;
        return $this;
    }

    public function getLatestReplierId(): ?int
    {
        return $this->latestReplierId;
    }

    public function setLatestReplierId(?int $value): self
    {
        $this->latestReplierId = $value;
        return $this;
    }

    public function getReplyCount(): int
    {
        return $this->replyCount ?? 0;
    }

    public function setReplyCount(?int $value): self
    {
        $this->replyCount = $value;
        return $this;
    }

    public function getViewCount(): int
    {
        return $this->viewCount ?? 0;
    }

    public function setViewCount(?int $value): self
    {
        $this->viewCount = $value;
        return $this;
    }

    public function getShareCount(): int
    {
        return $this->shareCount ?? 0;
    }

    public function setShareCount(?int $value): self
    {
        $this->shareCount = $value;
        return $this;
    }

    public function getZan(): ?array
    {
        return BlobResourceGetter::protoBuf($this->zan, Zan::class);
    }

    /** @param ?resource $zan */
    public function setZan($zan): self
    {
        $this->zan = $zan;
        return $this;
    }

    public function getGeolocation(): ?array
    {
        return BlobResourceGetter::protoBuf($this->geolocation, Lbs::class);
    }

    /** @param ?resource $geolocation */
    public function setGeolocation($geolocation): self
    {
        $this->geolocation = $geolocation;
        return $this;
    }

    public function getAuthorPhoneType(): ?string
    {
        return $this->authorPhoneType;
    }

    public function setAuthorPhoneType(?string $value): self
    {
        $this->authorPhoneType = $value;
        return $this;
    }
}
