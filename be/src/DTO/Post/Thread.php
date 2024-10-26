<?php

namespace App\DTO\Post;

use App\Entity\Post\Thread as ThreadEntity;
use Illuminate\Support\Collection;

class Thread extends ThreadEntity implements SortablePost
{
    use Post { fromEntity as private fromPostEntity; }

    private Collection $replies;

    public function getReplies(): Collection
    {
        return $this->replies;
    }

    public function setReplies(Collection $replies): self
    {
        $this->replies = $replies;
        return $this;
    }

    public static function fromEntity(ThreadEntity $entity): self
    {
        $dto = self::fromPostEntity($entity);
        $dto->tid = $entity->tid;
        $dto->threadType = $entity->threadType;
        $dto->stickyType = $entity->stickyType;
        $dto->topicType = $entity->topicType;
        $dto->isGood = $entity->isGood;
        $dto->title = $entity->title;
        $dto->latestReplyPostedAt = $entity->latestReplyPostedAt;
        $dto->latestReplierId = $entity->latestReplierId;
        $dto->replyCount = $entity->replyCount;
        $dto->viewCount = $entity->viewCount;
        $dto->shareCount = $entity->shareCount;
        $dto->zan = $entity->zan;
        $dto->geolocation = $entity->geolocation;
        $dto->authorPhoneType = $entity->authorPhoneType;
        return $dto;
    }
}
