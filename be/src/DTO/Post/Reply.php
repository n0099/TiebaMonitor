<?php

namespace App\DTO\Post;

use App\Entity\Post\Reply as ReplyEntity;
use Illuminate\Support\Collection;

class Reply extends ReplyEntity implements SortablePost
{
    use Post { fromEntity as private fromPostEntity; }
    use PostWithContent;

    private Collection $subReplies;

    public function getSubReplies(): Collection
    {
        return $this->subReplies;
    }

    public function setSubReplies(Collection $value): self
    {
        $this->subReplies = $value;
        return $this;
    }

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
