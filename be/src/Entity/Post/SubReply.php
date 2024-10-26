<?php

namespace App\Entity\Post;

use App\Repository\Post\SubReplyRepository;
use Doctrine\ORM\Mapping as ORM;

#[ORM\Entity(repositoryClass: SubReplyRepository::class)]
class SubReply extends Post
{
    #[ORM\Column] protected int $tid;
    #[ORM\Column] protected int $pid;
    #[ORM\Column, ORM\Id] protected int $spid;

    public function getTid(): int
    {
        return $this->tid;
    }

    public function setTid(int $value): self
    {
        $this->tid = $value;
        return $this;
    }

    public function getPid(): int
    {
        return $this->pid;
    }

    public function setPid(int $value): self
    {
        $this->pid = $value;
        return $this;
    }

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
