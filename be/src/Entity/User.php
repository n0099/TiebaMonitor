<?php

namespace App\Entity;

use App\Repository\UserRepository;
use Doctrine\ORM\Mapping as ORM;
use TbClient\Wrapper\UserIconWrapper;

#[ORM\Entity(repositoryClass: UserRepository::class)]
#[ORM\Table(name: '"tbmc_user"')]
class User extends TimestampedEntity
{
    #[ORM\Column, ORM\Id] protected int $uid;
    #[ORM\Column] protected ?string $name;
    /** @var ?resource */
    #[ORM\Column] protected $displayName;
    #[ORM\Column] protected string $portrait;
    #[ORM\Column] protected ?int $portraitUpdatedAt;
    #[ORM\Column] protected ?int $gender;
    #[ORM\Column] protected ?string $fansNickname;
    /** @var ?resource */
    #[ORM\Column] protected $icon;
    #[ORM\Column] protected ?string $ipGeolocation;

    public function getUid(): ?int
    {
        return $this->uid;
    }

    public function setUid(int $value): self
    {
        $this->uid = $value;
        return $this;
    }

    public function getName(): ?string
    {
        return $this->name;
    }

    public function setName(?string $value): self
    {
        $this->name = $value;
        return $this;
    }

    public function getDisplayName(): ?string
    {
        return BlobResourceGetter::resource($this->displayName);
    }

    /** @param ?resource $displayName */
    public function setDisplayName($displayName): self
    {
        $this->displayName = $displayName;
        return $this;
    }

    public function getPortrait(): string
    {
        return $this->portrait;
    }

    public function setPortrait(string $value): self
    {
        $this->portrait = $value;
        return $this;
    }

    public function getPortraitUpdatedAt(): ?int
    {
        return $this->portraitUpdatedAt;
    }

    public function setPortraitUpdatedAt(?int $value): self
    {
        $this->portraitUpdatedAt = $value;
        return $this;
    }

    public function getGender(): ?int
    {
        return $this->gender;
    }

    public function setGender(?int $value): self
    {
        $this->gender = $value;
        return $this;
    }

    public function getFansNickname(): ?string
    {
        return $this->fansNickname;
    }

    public function setFansNickname(?string $value): self
    {
        $this->fansNickname = $value;
        return $this;
    }

    public function getIcon(): ?array
    {
        return BlobResourceGetter::protoBufWrapper($this->icon, UserIconWrapper::class);
    }

    /** @param ?resource $icon */
    public function setIcon($icon): self
    {
        $this->icon = $icon;
        return $this;
    }

    public function getIpGeolocation(): ?string
    {
        return $this->ipGeolocation;
    }

    public function setIpGeolocation(?string $value): self
    {
        $this->ipGeolocation = $value;
        return $this;
    }
}
