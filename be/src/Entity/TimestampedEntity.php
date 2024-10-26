<?php

namespace App\Entity;

use Doctrine\ORM\Mapping as ORM;

#[ORM\MappedSuperclass]
abstract class TimestampedEntity
{
    #[ORM\Column] protected int $createdAt;
    #[ORM\Column] protected ?int $updatedAt;

    public function getCreatedAt(): int
    {
        return $this->createdAt;
    }

    public function setCreatedAt(int $value): self
    {
        $this->createdAt = $value;
        return $this;
    }

    public function getUpdatedAt(): ?int
    {
        return $this->updatedAt;
    }

    public function setUpdatedAt(?int $value): self
    {
        $this->updatedAt = $value;
        return $this;
    }
}
