<?php

namespace App\Tests\EventListener;

use App\EventListener\ShowReactJsonView;
use PHPUnit\Framework\Attributes\CoversClass;
use Symfony\Bundle\FrameworkBundle\Test\KernelTestCase;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpKernel\Event\ResponseEvent;
use Symfony\Component\HttpKernel\HttpKernelInterface;

#[CoversClass(ShowReactJsonView::class)]
class ShowReactJsonViewTest extends KernelTestCase
{
    private ShowReactJsonView $sut;

    protected function setUp(): void
    {
        parent::setUp();
        self::bootKernel();
        $container = static::getContainer();
        $this->sut = $container->get(ShowReactJsonView::class);
    }

    protected function tearDown(): void
    {
        parent::tearDown();
    }

    public function testShowing(): void
    {
        $event = new ResponseEvent(
            self::$kernel,
            new Request(server: ['HTTP_ACCEPT' => 'text/html']),
            HttpKernelInterface::MAIN_REQUEST,
            JsonResponse::fromJsonString(\Safe\json_encode(['test' => 'test'])),
        );
        ($this->sut)($event);
        self::assertEquals(<<<'HTML'
        <html>
            <head>
                <title>15 bytes of json response for route /</title>
            </head>
            <body>
                <h4>15 bytes</h4>
                <div id="root"></div>
                <script type="module">
                    import ReactJsonView from '/assets/react-json-view.js';
                    import { createElement } from '/assets/react.js';
                    import { createRoot } from '/assets/react-dom.js';

                    const root = createRoot(document.getElementById('root'));
                    root.render(createElement(ReactJsonView.default, { src: {"test":"test"}, quotesOnKeys: false }));
                </script>
                <style>
                    .object-content {
                        content-visibility: auto;
                    }
                </style>
            </body>
        </html>
        HTML, $event->getResponse()->getContent());
    }

    public function testNotShowing(): void
    {
        $event = new ResponseEvent(
            self::$kernel,
            new Request(),
            HttpKernelInterface::MAIN_REQUEST,
            JsonResponse::fromJsonString(\Safe\json_encode(['test' => 'test'])),
        );
        ($this->sut)($event);
        self::assertEquals('{"test":"test"}', $event->getResponse()->getContent());
    }
}
