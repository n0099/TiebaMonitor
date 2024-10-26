<?php

namespace App\DTO\Post;

use Symfony\Component\Serializer\Attribute\Ignore;

interface SortablePost
{
    public function getIsMatchQuery(): bool;

    public function setIsMatchQuery(bool $isMatchQuery): void;

    #[Ignore] public function getSortingKey(): mixed;

    public function setSortingKey(mixed $sortingKey): void;
}
