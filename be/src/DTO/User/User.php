<?php

namespace App\DTO\User;

use App\DTO\TimestampedDTO;
use App\Entity\User as UserEntity;

class User extends UserEntity
{
    use TimestampedDTO { fromEntity as private fromTimestampedEntity; }

    private ?ForumModerator $currentForumModerator;
    private ?AuthorExpGrade $currentAuthorExpGrade;

    public function getCurrentForumModerator(): ?ForumModerator
    {
        return $this->currentForumModerator;
    }

    public function setCurrentForumModerator(?ForumModerator $currentForumModerator): void
    {
        $this->currentForumModerator = $currentForumModerator;
    }

    public function getCurrentAuthorExpGrade(): ?AuthorExpGrade
    {
        return $this->currentAuthorExpGrade;
    }

    public function setCurrentAuthorExpGrade(?AuthorExpGrade $currentAuthorExpGrade): void
    {
        $this->currentAuthorExpGrade = $currentAuthorExpGrade;
    }

    public static function fromEntity(UserEntity $entity): self
    {
        $dto = self::fromTimestampedEntity($entity);
        $dto->setUid($entity->getUid());
        $dto->setName($entity->getName());
        $dto->displayName = $entity->displayName;
        $dto->setPortrait($entity->getPortrait());
        $dto->setPortraitUpdatedAt($entity->getPortraitUpdatedAt());
        $dto->setGender($entity->getGender());
        $dto->setFansNickname($entity->getFansNickname());
        $dto->icon = $entity->icon;
        $dto->setIpGeolocation($entity->getIpGeolocation());
        return $dto;
    }
}
