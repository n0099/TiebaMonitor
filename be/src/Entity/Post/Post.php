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

    public function setAuthorUid(int $value): self
    {
        $this->authorUid = $value;
        return $this;
    }

    public function getPostedAt(): int
    {
        return $this->postedAt;
    }

    public function setPostedAt(int $value): self
    {
        $this->postedAt = $value;
        return $this;
    }

    public function getLastSeenAt(): ?int
    {
        return $this->lastSeenAt;
    }

    public function setLastSeenAt(?int $value): self
    {
        $this->lastSeenAt = $value;
        return $this;
    }

    public function getAgreeCount(): int
    {
        return $this->agreeCount ?? 0;
    }

    public function setAgreeCount(?int $value): self
    {
        $this->agreeCount = $value;
        return $this;
    }

    public function getDisagreeCount(): int
    {
        return $this->disagreeCount ?? 0;
    }

    public function setDisagreeCount(?int $value): self
    {
        $this->disagreeCount = $value;
        return $this;
    }
}
