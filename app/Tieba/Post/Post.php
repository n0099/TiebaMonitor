<?php

namespace App\Tieba\Post;

use Illuminate\Database\Eloquent\Model;
use function GuzzleHttp\json_decode;

abstract class Post
{
    public static function convertJsonContentToHtml(array $content): string
    {
        // remove spam \n and spaces due to blade @break directive
        return str_replace("\n", null, trim(view('formatPostJsonContent', ['content' => $content])));
    }

    /**
     * Create a post helper with PostModel or array
     *
     * @param array|\Illuminate\Database\Eloquent\Model $postData
     */
    public function __construct($postData)
    {
        $initialWith = function ($postProperties) {
            $postIDByType = [
                Thread::class => 'tid',
                Reply::class => 'pid',
                SubReply::class => 'spid',
            ];
            if (! isset($postProperties[$postIDByType[get_class($this)]])) {
                throw new \DomainException('Initial object dosen\'t match with class type');
            }

            foreach ($postProperties as $postPropertyName => $postPropertyValue) {
                $this->$postPropertyName = $postPropertyValue;
            }
        };

        if ($postData instanceof Model) {
            /*if (count($postData) > 1) {
                throw new \LengthException('Initial collection can\'t larger than one element');
            }*/

            $initialWith($postData->toArray());
        } elseif (is_array($postData)) {
            $initialWith($postData);
        } else {
            throw new \InvalidArgumentException('Unexpected initial object: ' . gettype($postData));
        }
    }
}