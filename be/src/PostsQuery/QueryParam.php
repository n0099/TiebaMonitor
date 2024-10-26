<?php

namespace App\PostsQuery;

class QueryParam
{
    public readonly string $name;

    public array|string|int $value;

    protected array $subParams;

    public function __construct(array $param)
    {
        $this->name = (string) array_keys($param)[0];
        if (is_numeric($this->name)) {
            throw new \InvalidArgumentException();
        }
        $this->value = $param[$this->name];
        array_shift($param);
        $this->subParams = $param;
    }

    public function getAllSub(): array
    {
        return $this->subParams;
    }

    public function getSub(string $name)
    {
        return $this->subParams[$name] ?? null;
    }

    public function setSub(string $name, array|string|int $value): self
    {
        $this->subParams[$name] = $value;
        return $this;
    }
}
