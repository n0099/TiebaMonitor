<?php

namespace App\Tests\EventListener;

use App\EventListener\SerializeToJson;
use PHPUnit\Framework\Attributes\CoversClass;
use PHPUnit\Framework\Attributes\DataProvider;
use Symfony\Bundle\FrameworkBundle\Test\KernelTestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpKernel\Event\ViewEvent;
use Symfony\Component\HttpKernel\HttpKernelInterface;

#[CoversClass(SerializeToJson::class)]
class SerializeToJsonTest extends KernelTestCase
{
    private SerializeToJson $sut;

    protected function setUp(): void
    {
        parent::setUp();
        self::bootKernel();
        $this->sut = static::getContainer()->get(SerializeToJson::class);
    }

    #[DataProvider('provide')]
    public function test($provided, string $expected)
    {
        $event = new ViewEvent(self::$kernel, new Request(), HttpKernelInterface::MAIN_REQUEST, $provided);
        ($this->sut)($event);
        self::assertEquals($expected, $event->getResponse()->getContent());
    }

    public static function provide(): array
    {
        return [
            [['test' => true], '{"test":true}'],
            [collect(['test' => true]), '{"test":true}'],
        ];
    }
}
