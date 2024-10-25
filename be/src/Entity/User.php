<?php

namespace App\Entity;

use App\Repository\UserRepository;
use Doctrine\ORM\Mapping as ORM;
use TbClient\Wrapper\UserIconWrapper;

#[ORM\Entity(repositoryClass: UserRepository::class)]
#[ORM\Table(name: '"tbmc_user"')]
class User extends TimestampedEntity
{
    #[ORM\Column, ORM\Id] private int $uid;
    #[ORM\Column] private ?string $name;
    /** @var ?resource */
    #[ORM\Column] protected $displayName;
    #[ORM\Column] private string $portrait;
    #[ORM\Column] private ?int $portraitUpdatedAt;
    #[ORM\Column] private ?int $gender;
    #[ORM\Column] private ?string $fansNickname;
    /** @var ?resource */
    #[ORM\Column] protected $icon;
    #[ORM\Column] private ?string $ipGeolocation;

    public function getUid(): ?int
    {
        return $this->uid;
    }

    public function setUid(int $uid): void
    {
        $this->uid = $uid;
    }

    public function getName(): ?string
    {
        return $this->name;
    }

    public function setName(?string $name): void
    {
        $this->name = $name;
    }

    public function getDisplayName(): ?string
    {
        return BlobResourceGetter::resource($this->displayName);
    }

    /** @param ?resource $displayName */
    public function setDisplayName($displayName): void
    {
        $this->displayName = $displayName;
    }

    public function getPortrait(): string
    {
        return $this->portrait;
    }

    public function setPortrait(string $portrait): void
    {
        $this->portrait = $portrait;
    }

    public function getPortraitUpdatedAt(): ?int
    {
        return $this->portraitUpdatedAt;
    }

    public function setPortraitUpdatedAt(?int $portraitUpdatedAt): void
    {
        $this->portraitUpdatedAt = $portraitUpdatedAt;
    }

    public function getGender(): ?int
    {
        return $this->gender;
    }

    public function setGender(?int $gender): void
    {
        $this->gender = $gender;
    }

    public function getFansNickname(): ?string
    {
        return $this->fansNickname;
    }

    public function setFansNickname(?string $fansNickname): void
    {
        $this->fansNickname = $fansNickname;
    }

    public function getIcon(): ?array
    {
        return BlobResourceGetter::protoBufWrapper($this->icon, UserIconWrapper::class);
    }

    /** @param ?resource $icon */
    public function setIcon($icon): void
    {
        $this->icon = $icon;
    }

    public function getIpGeolocation(): ?string
    {
        return $this->ipGeolocation;
    }

    public function setIpGeolocation(?string $ipGeolocation): void
    {
        $this->ipGeolocation = $ipGeolocation;
    }
}
