<?php

namespace App\Eloquent\Model\Post;

use App\Eloquent\NullableBooleanAttributeCast;
use App\Eloquent\NullableNumericAttributeCast;
use Illuminate\Database\Eloquent\Builder;
use Illuminate\Database\Eloquent\Casts\Attribute;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Support\Collection;
use TbClient\Post\Common\Lbs;

class ReplyModel extends PostModel
{
    protected $primaryKey = 'pid';

    protected $casts = [
        'subReplyCount' => NullableNumericAttributeCast::class,
        'isFold' => NullableBooleanAttributeCast::class,
        'agreeCount' => NullableNumericAttributeCast::class,
        'disagreeCount' => NullableNumericAttributeCast::class
    ];

    public function __construct(array $attributes = [])
    {
        parent::__construct($attributes);
        $this->publicFields = [
            'tid',
            'pid',
            'floor',
            'authorUid',
            'subReplyCount',
            'postedAt',
            'isFold',
            'agreeCount',
            'disagreeCount',
            'geolocation',
            'signatureId',
            ...parent::TIMESTAMP_FIELDS
        ];
    }

    protected function geolocation(): Attribute
    {
        return self::makeProtoBufAttribute(Lbs::class);
    }

    /**
     * @psalm-return BelongsTo<ThreadModel>
     */
    public function thread(): BelongsTo
    {
        return $this->belongsTo(ThreadModel::class, 'tid', 'tid');
    }

    /**
     * @psalm-return HasMany<SubReplyModel>
     */
    public function subReplies(): HasMany
    {
        return $this->hasMany(SubReplyModel::class, 'pid', 'pid');
    }

    public function scopePid(Builder $query, Collection|array|int $pid): Builder
    {
        return $this->scopeIDType($query, 'pid', $pid);
    }
}