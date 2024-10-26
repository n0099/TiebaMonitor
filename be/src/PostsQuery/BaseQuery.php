<?php

namespace App\PostsQuery;

abstract readonly class BaseQuery
{
    public string $orderByField;

    public bool $orderByDesc;

    public function __construct(
        public QueryResult $queryResult,
        public PostsTree $postsTree,
    ) {}

    abstract public function query(QueryParams $params, ?string $cursor): void;

    protected function setOrderByField(string $value): self
    {
        $this->orderByField = $value;
        return $this;
    }

    protected function setOrderByDesc(bool $value): self
    {
        $this->orderByDesc = $value;
        return $this;
    }
}
