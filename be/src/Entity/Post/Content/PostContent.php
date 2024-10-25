<?php

namespace App\Entity\Post\Content;

use App\Entity\BlobResourceGetter;
use Doctrine\ORM\Mapping as ORM;
use TbClient\Wrapper\PostContentWrapper;

#[ORM\MappedSuperclass]
abstract class PostContent
{
    /** @var ?resource */
    #[ORM\Column] private $protoBufBytes;

    public function getContent(): ?array
    {
        return BlobResourceGetter::protoBufWrapper($this->protoBufBytes, PostContentWrapper::class);
    }

    /** @param ?resource $protoBufBytes */
    public function setProtoBufBytes($protoBufBytes): void
    {
        $this->protoBufBytes = $protoBufBytes;
    }
}
