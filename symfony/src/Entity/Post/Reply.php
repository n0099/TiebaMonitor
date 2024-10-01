<?php /** @noinspection PhpPropertyOnlyWrittenInspection */

namespace App\Entity\Post;

use App\Entity\BlobResourceGetter;
use App\Repository\Post\ReplyRepository;
use Doctrine\ORM\Mapping as ORM;
use TbClient\Post\Common\Lbs;

#[ORM\Entity(repositoryClass: ReplyRepository::class)]
class Reply extends Post
{
    #[ORM\Column] private int $tid;
    #[ORM\Column, ORM\Id] private int $pid;
    #[ORM\Column] private int $floor;
    #[ORM\Column] private ?int $subReplyCount;
    #[ORM\Column] private ?int $isFold;
    /** @type resource|null */
    #[ORM\Column] private $geolocation;
    #[ORM\Column] private ?int $signatureId;

    public function getTid(): int
    {
        return $this->tid;
    }

    public function getPid(): int
    {
        return $this->pid;
    }

    public function getFloor(): int
    {
        return $this->floor;
    }

    public function getSubReplyCount(): ?int
    {
        return $this->subReplyCount;
    }

    public function getIsFold(): ?int
    {
        return $this->isFold;
    }

    public function getGeolocation(): ?\stdClass
    {
        return BlobResourceGetter::protoBuf($this->geolocation, Lbs::class);
    }

    public function getSignatureId(): ?int
    {
        return $this->signatureId;
    }
}
