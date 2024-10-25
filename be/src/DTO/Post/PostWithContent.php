<?php

namespace App\DTO\Post;

trait PostWithContent
{
    private ?array $content;

    public function getContent(): ?array
    {
        return $this->content;
    }

    public function setContent(?array $content): void
    {
        $this->content = $content;
    }
}
