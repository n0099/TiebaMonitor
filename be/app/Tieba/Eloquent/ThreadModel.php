<?php

namespace App\Tieba\Eloquent;

use Illuminate\Database\Eloquent\Casts\Attribute;
use Illuminate\Database\Eloquent\Relations\HasMany;
use TbClient\Post\Common\Lbs;
use TbClient\Post\Common\Zan;

/**
 * Model for every Tieba thread post
 *
 * @package App\Tieba\Eloquent
 */
class ThreadModel extends PostModel
{
    protected $primaryKey = 'tid';

    protected static array $fields = [
        'tid',
        'threadType',
        'stickyType',
        'topicType',
        'isGood',
        'title',
        'authorUid',
        'postedAt',
        'latestReplyPostedAt',
        'latestReplierUid',
        'replyCount',
        'viewCount',
        'shareCount',
        'agreeCount',
        'disagreeCount',
        'zan',
        'geolocation',
        'authorPhoneType',
        ...parent::TIMESTAMP_FIELDS
    ];

    protected $casts = [
        'isGood' => NullableBooleanAttributeCast::class,
        'replyCount' => NullableNumericAttributeCast::class,
        'viewCount' => NullableNumericAttributeCast::class,
        'shareCount' => NullableNumericAttributeCast::class,
        'agreeCount' => NullableNumericAttributeCast::class,
        'disagreeCount' => NullableNumericAttributeCast::class
    ];

    protected function zan(): Attribute
    {
        return self::makeProtoBufAttribute(Zan::class);
    }

    protected function geolocation(): Attribute
    {
        return self::makeProtoBufAttribute(Lbs::class);
    }

    /**
     * @psalm-return HasMany<ReplyModel>
     */
    public function replies(): HasMany
    {
        return $this->hasMany(ReplyModel::class, 'tid', 'tid');
    }
}
