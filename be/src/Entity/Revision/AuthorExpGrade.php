<?php

namespace App\Entity\Revision;

use App\Repository\Revision\AuthorExpGradeRepository;
use Doctrine\ORM\Mapping as ORM;

#[ORM\Entity(repositoryClass: AuthorExpGradeRepository::class)]
#[ORM\Table(name: '"tbmcr_authorExpGrade"')]
class AuthorExpGrade
{
    #[ORM\Column, ORM\Id] private int $discoveredAt;
    #[ORM\Column, ORM\Id] private int $fid;
    #[ORM\Column, ORM\Id] private int $uid;
    #[ORM\Column] private string $triggeredBy;
    #[ORM\Column] private int $authorExpGrade;

    public function getDiscoveredAt(): int
    {
        return $this->discoveredAt;
    }

    public function setDiscoveredAt(int $value): self
    {
        $this->discoveredAt = $value;
        return $this;
    }

    public function getFid(): int
    {
        return $this->fid;
    }

    public function setFid(int $value): self
    {
        $this->fid = $value;
        return $this;
    }

    public function getUid(): int
    {
        return $this->uid;
    }

    public function setUid(int $value): self
    {
        $this->uid = $value;
        return $this;
    }

    public function getTriggeredBy(): string
    {
        return $this->triggeredBy;
    }

    public function setTriggeredBy(string $value): self
    {
        $this->triggeredBy = $value;
        return $this;
    }

    public function getAuthorExpGrade(): int
    {
        return $this->authorExpGrade;
    }

    public function setAuthorExpGrade(int $value): self
    {
        $this->authorExpGrade = $value;
        return $this;
    }
}
