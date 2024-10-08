<?php /** @noinspection PhpPropertyOnlyWrittenInspection */

namespace App\Entity\Post;

use App\Repository\Post\SubReplyRepository;
use Doctrine\ORM\Mapping as ORM;

#[ORM\Entity(repositoryClass: SubReplyRepository::class)]
class SubReply extends PostWithContent
{
    #[ORM\Column] private int $tid;
    #[ORM\Column] private int $pid;
    #[ORM\Column, ORM\Id] private int $spid;

    public function getTid(): int
    {
        return $this->tid;
    }

    public function getPid(): int
    {
        return $this->pid;
    }

    public function getSpid(): int
    {
        return $this->spid;
    }
}
