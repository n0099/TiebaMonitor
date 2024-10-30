<?php

namespace App\Tests\EventListener;

use App\EventListener\PrettyJsonResponse;
use PHPUnit\Framework\Attributes\CoversClass;
use Symfony\Bundle\FrameworkBundle\Test\KernelTestCase;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpKernel\Event\ResponseEvent;
use Symfony\Component\HttpKernel\HttpKernelInterface;

#[CoversClass(PrettyJsonResponse::class)]
class PrettyJsonResponseTest extends KernelTestCase
{
    private PrettyJsonResponse $sut;

    protected function setUp(): void
    {
        parent::setUp();
        self::bootKernel();
        $this->sut = static::getContainer()->get(PrettyJsonResponse::class);
    }

    public function test(): void
    {
        $event = new ResponseEvent(
            self::$kernel,
            new Request(server: ['HTTP_ACCEPT' => 'application/json']),
            HttpKernelInterface::MAIN_REQUEST,
            JsonResponse::fromJsonString(\Safe\json_encode(['test' => 'test'])),
        );
        ($this->sut)($event);
        self::assertEquals(<<<'JSON'
        {
            "test": "test"
        }
        JSON, $event->getResponse()->getContent());
    }
}
