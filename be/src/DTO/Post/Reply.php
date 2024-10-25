<?php

namespace App\DTO\Post;

use App\Entity\Post\Reply as ReplyEntity;

class Reply extends ReplyEntity
{
    use Post { fromEntity as private fromPostEntity; }
    use PostWithContent;

    public static function fromEntity(ReplyEntity $entity): self
    {
        $dto = self::fromPostEntity($entity);
        $dto->setTid($entity->getTid());
        $dto->setPid($entity->getPid());
        $dto->setFloor($entity->getFloor());
        $dto->setSubReplyCount($entity->getSubReplyCount());
        $dto->setIsFold($entity->getIsFold());
        $dto->geolocation = $entity->geolocation;
        $dto->setSignatureId($entity->getSignatureId());
        return $dto;
    }
}
