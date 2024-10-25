<?php

namespace App\DTO\Post;

use App\DTO\TimestampedDTO;

trait Post
{
    use TimestampedDTO { fromEntity as private fromTimestampedEntity; }
    use PostWithIsMatchQuery;

    public static function fromEntity(\App\Entity\Post\Post $entity): self
    {
        $dto = self::fromTimestampedEntity($entity);
        $dto->setAuthorUid($entity->getAuthorUid());
        $dto->setPostedAt($entity->getPostedAt());
        $dto->setLastSeenAt($entity->getLastSeenAt());
        $dto->setAgreeCount($entity->getAgreeCount());
        $dto->setDisagreeCount($entity->getDisagreeCount());
        return $dto;
    }
}
