<?php

namespace App\Entity\Post;

use App\Entity\TimestampedEntity;
use Doctrine\ORM\Mapping as ORM;

#[ORM\MappedSuperclass]
abstract class Post extends TimestampedEntity
{
    #[ORM\Column] protected int $authorUid;
    #[ORM\Column] protected int $postedAt;
    #[ORM\Column] protected ?int $lastSeenAt;
    #[ORM\Column] protected ?int $agreeCount;
    #[ORM\Column] protected ?int $disagreeCount;

    public function getAuthorUid(): int
    {
        return $this->authorUid;
    }

    public function setAuthorUid(int $authorUid): void
    {
        $this->authorUid = $authorUid;
    }

    public function getPostedAt(): int
    {
        return $this->postedAt;
    }

    public function setPostedAt(int $postedAt): void
    {
        $this->postedAt = $postedAt;
    }

    public function getLastSeenAt(): ?int
    {
        return $this->lastSeenAt;
    }

    public function setLastSeenAt(?int $lastSeenAt): void
    {
        $this->lastSeenAt = $lastSeenAt;
    }

    public function getAgreeCount(): int
    {
        return $this->agreeCount ?? 0;
    }

    public function setAgreeCount(?int $agreeCount): void
    {
        $this->agreeCount = $agreeCount;
    }

    public function getDisagreeCount(): int
    {
        return $this->disagreeCount ?? 0;
    }

    public function setDisagreeCount(?int $disagreeCount): void
    {
        $this->disagreeCount = $disagreeCount;
    }
}
