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
        $dto->tid = $entity->tid;
        $dto->pid = $entity->pid;
        $dto->floor = $entity->floor;
        $dto->subReplyCount = $entity->subReplyCount;
        $dto->isFold = $entity->isFold;
        $dto->geolocation = $entity->geolocation;
        $dto->signatureId = $entity->signatureId;
        return $dto;
    }
}
