<?php

namespace App\Entity;

use App\Repository\ForumRepository;
use Doctrine\ORM\Mapping as ORM;

#[ORM\Entity(repositoryClass: ForumRepository::class)]
#[ORM\Table(name: '"tbm_forum"')]
class Forum
{
    #[ORM\Column, ORM\Id] private int $fid;
    #[ORM\Column] private string $name;
    #[ORM\Column] private bool $isCrawling;

    public function getFid(): int
    {
        return $this->fid;
    }

    public function setFid(int $value): self
    {
        $this->fid = $value;
        return $this;
    }

    public function getName(): string
    {
        return $this->name;
    }

    public function setName(string $value): self
    {
        $this->name = $value;
        return $this;
    }

    public function isCrawling(): bool
    {
        return $this->isCrawling;
    }

    public function setIsCrawling(bool $value): self
    {
        $this->isCrawling = $value;
        return $this;
    }
}
