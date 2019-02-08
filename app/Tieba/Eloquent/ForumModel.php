<?php

namespace App\Tieba\Eloquent;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Builder;

class ForumModel extends Model
{
    protected $table = 'tbm_forumsInfo';

    protected $guarded = [];

    protected $fields = [
        'id',
        'fid',
        'name'
    ];

    protected $hidedFields = [
        'id',
    ];

    public function scopeHidePrivateFields(Builder $query): Builder
    {
        return $query->select(array_diff($this->fields, $this->hidedFields));
    }

    public static function getName(int $fid): \Illuminate\Support\Collection
    {
        return self::where('fid', $fid)->value('name');
    }

    public static function getFid(string $fourmName): \Illuminate\Support\Collection
    {
        return self::where('name', $fourmName)->value('fid');
    }
}