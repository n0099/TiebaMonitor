<?php

namespace App\DTO\Post;

trait PostWithContent
{
    private ?array $content;

    public function getContent(): ?array
    {
        return $this->content;
    }

    public function setContent(?array $value): self
    {
        $this->content = $value;
        return $this;
    }
}
