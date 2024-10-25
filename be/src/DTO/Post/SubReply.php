<?php

namespace App\DTO\Post;

use App\Entity\Post\SubReply as SubReplyEntity;
use Symfony\Component\Serializer\Attribute\Ignore;

class SubReply extends SubReplyEntity
{
    use Post { fromEntity as private fromPostEntity; }
    use PostWithContent;

    public static function fromEntity(SubReplyEntity $entity): self
    {
        $dto = self::fromPostEntity($entity);
        $dto->setTid($entity->getTid());
        $dto->setPid($entity->getPid());
        $dto->setSpid($entity->getSpid());
        return $dto;
    }

    #[Ignore]
    public function getIsMatchQuery(): bool
    {
        return true;
    }

    public function setIsMatchQuery(bool $isMatchQuery): void {}
}
