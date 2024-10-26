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

    public function setCurrentForumModerator(?ForumModerator $value): self
    {
        $this->currentForumModerator = $value;
        return $this;
    }

    public function getCurrentAuthorExpGrade(): ?AuthorExpGrade
    {
        return $this->currentAuthorExpGrade;
    }

    public function setCurrentAuthorExpGrade(?AuthorExpGrade $value): self
    {
        $this->currentAuthorExpGrade = $value;
        return $this;
    }

    public static function fromEntity(UserEntity $entity): self
    {
        $dto = self::fromTimestampedEntity($entity);
        $dto->uid = $entity->uid;
        $dto->name = $entity->name;
        $dto->displayName = $entity->displayName;
        $dto->portrait = $entity->portrait;
        $dto->portraitUpdatedAt = $entity->portraitUpdatedAt;
        $dto->gender = $entity->gender;
        $dto->fansNickname = $entity->fansNickname;
        $dto->icon = $entity->icon;
        $dto->ipGeolocation = $entity->ipGeolocation;
        return $dto;
    }
}
