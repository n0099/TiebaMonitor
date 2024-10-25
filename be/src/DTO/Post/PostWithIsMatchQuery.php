<?php

namespace App\DTO\Post;

trait PostWithIsMatchQuery
{
    private bool $isMatchQuery;

    public function getIsMatchQuery(): bool
    {
        return $this->isMatchQuery;
    }

    public function setIsMatchQuery(bool $isMatchQuery): void
    {
        $this->isMatchQuery = $isMatchQuery;
    }
}
