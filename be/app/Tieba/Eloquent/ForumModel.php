<?php

namespace App\Tieba\Eloquent;

use Illuminate\Database\Eloquent\Builder;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Support\Collection;

class ForumModel extends Model
{
    protected $table = 'tbm_forumsInfo';

    protected $guarded = [];

    protected array $fields = [
        'id',
        'fid',
        'name'
    ];

    protected array $hidedFields = [
        'id'
    ];

    public function scopeHidePrivateFields(Builder $query): Builder
    {
        return $query->select(array_diff($this->fields, $this->hidedFields));
    }

    public function scopeIsCrawling(Builder $query, bool $isCrawling): Builder
    {
        return $query->where('isCrawling', $isCrawling);
    }

    public static function getName(int $fid): Collection
    {
        return self::where('fid', $fid)->value('name');
    }

    public static function getFid(string $forumName): Collection
    {
        return self::where('name', $forumName)->value('fid');
    }
}