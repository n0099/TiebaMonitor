<?php

namespace App\DTO\Post;

use App\Entity\Post\Thread as ThreadEntity;

class Thread extends ThreadEntity
{
    use Post { fromEntity as private fromPostEntity; }

    public static function fromEntity(ThreadEntity $entity): self
    {
        $dto = self::fromPostEntity($entity);
        $dto->setTid($entity->getTid());
        $dto->setThreadType($entity->getThreadType());
        $dto->setStickyType($entity->getStickyType());
        $dto->setTopicType($entity->getTopicType());
        $dto->setIsGood($entity->getIsGood());
        $dto->setTitle($entity->getTitle());
        $dto->setLatestReplyPostedAt($entity->getLatestReplyPostedAt());
        $dto->setLatestReplierId($entity->getLatestReplierId());
        $dto->setReplyCount($entity->getReplyCount());
        $dto->setViewCount($entity->getViewCount());
        $dto->setShareCount($entity->getShareCount());
        $dto->zan = $entity->zan;
        $dto->geolocation = $entity->geolocation;
        $dto->setAuthorPhoneType($entity->getAuthorPhoneType());
        return $dto;
    }
}
