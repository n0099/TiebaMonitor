<?php

namespace App\DTO;

use App\Entity\TimestampedEntity;

trait TimestampedDTO
{
    public static function fromEntity(TimestampedEntity $entity): self
    {
        $dto = new self();
        $dto->setCreatedAt($entity->getCreatedAt());
        $dto->setUpdatedAt($entity->getUpdatedAt());
        return $dto;
    }
}
