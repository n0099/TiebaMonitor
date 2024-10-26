<?php

namespace App\DTO\Post;

use App\DTO\TimestampedDTO;
use Symfony\Component\Serializer\Attribute\Ignore;

trait Post
{
    use TimestampedDTO { fromEntity as private fromTimestampedEntity; }

    private bool $isMatchQuery;
    private mixed $sortingKey = null;

    public function getIsMatchQuery(): bool
    {
        return $this->isMatchQuery;
    }

    public function setIsMatchQuery(bool $isMatchQuery): void
    {
        $this->isMatchQuery = $isMatchQuery;
    }

    #[Ignore] public function getSortingKey(): mixed
    {
        return $this->sortingKey;
    }

    public function setSortingKey(mixed $sortingKey): void
    {
        $this->sortingKey = $sortingKey;
    }

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
