<?php

namespace App\DTO;

use App\Entity\TimestampedEntity;

trait TimestampedDTO
{
    public static function fromEntity(TimestampedEntity $entity): self
    {
        $dto = new self();
        $dto->createdAt = $entity->createdAt;
        $dto->updatedAt = $entity->updatedAt;
        return $dto;
    }
}
