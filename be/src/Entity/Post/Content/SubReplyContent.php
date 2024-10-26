<?php

namespace App\Entity\Post\Content;

use App\Repository\Post\Content\SubReplyContentRepository;
use Doctrine\ORM\Mapping as ORM;

#[ORM\Entity(repositoryClass: SubReplyContentRepository::class)]
class SubReplyContent extends PostContent
{
    #[ORM\Column, ORM\Id] private int $spid;

    public function getSpid(): int
    {
        return $this->spid;
    }

    public function setSpid(int $value): self
    {
        $this->spid = $value;
        return $this;
    }
}
