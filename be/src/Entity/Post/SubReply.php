<?php

namespace App\Entity\Post;

use App\Repository\Post\SubReplyRepository;
use Doctrine\ORM\Mapping as ORM;

#[ORM\Entity(repositoryClass: SubReplyRepository::class)]
class SubReply extends Post
{
    #[ORM\Column] private int $tid;
    #[ORM\Column] private int $pid;
    #[ORM\Column, ORM\Id] private int $spid;

    public function getTid(): int
    {
        return $this->tid;
    }

    public function setTid(int $tid): void
    {
        $this->tid = $tid;
    }

    public function getPid(): int
    {
        return $this->pid;
    }

    public function setPid(int $pid): void
    {
        $this->pid = $pid;
    }

    public function getSpid(): int
    {
        return $this->spid;
    }

    public function setSpid(int $spid): void
    {
        $this->spid = $spid;
    }
}
