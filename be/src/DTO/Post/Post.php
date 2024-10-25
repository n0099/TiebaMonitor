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
        $dto->authorUid = $entity->authorUid;
        $dto->postedAt = $entity->postedAt;
        $dto->lastSeenAt = $entity->lastSeenAt;
        $dto->agreeCount = $entity->agreeCount;
        $dto->disagreeCount = $entity->disagreeCount;
        return $dto;
    }
}
