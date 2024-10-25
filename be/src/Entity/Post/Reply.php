<?php

namespace App\Entity\Post;

use App\Entity\BlobResourceGetter;
use App\Repository\Post\ReplyRepository;
use Doctrine\ORM\Mapping as ORM;
use TbClient\Post\Common\Lbs;

#[ORM\Entity(repositoryClass: ReplyRepository::class)]
class Reply extends Post
{
    #[ORM\Column] protected int $tid;
    #[ORM\Column, ORM\Id] protected int $pid;
    #[ORM\Column] protected int $floor;
    #[ORM\Column] protected ?int $subReplyCount;
    #[ORM\Column] protected ?int $isFold;
    /** @var ?resource */
    #[ORM\Column] protected $geolocation;
    #[ORM\Column] protected ?int $signatureId;

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

    public function getFloor(): int
    {
        return $this->floor;
    }

    public function setFloor(int $floor): void
    {
        $this->floor = $floor;
    }

    public function getSubReplyCount(): int
    {
        return $this->subReplyCount ?? 0;
    }

    public function setSubReplyCount(?int $subReplyCount): void
    {
        $this->subReplyCount = $subReplyCount;
    }

    public function getIsFold(): ?int
    {
        return $this->isFold;
    }

    public function setIsFold(?int $isFold): void
    {
        $this->isFold = $isFold;
    }

    public function getGeolocation(): ?array
    {
        return BlobResourceGetter::protoBuf($this->geolocation, Lbs::class);
    }

    /** @param ?resource $geolocation */
    public function setGeolocation($geolocation): void
    {
        $this->geolocation = $geolocation;
    }

    public function getSignatureId(): ?int
    {
        return $this->signatureId;
    }

    public function setSignatureId(?int $signatureId): void
    {
        $this->signatureId = $signatureId;
    }
}
