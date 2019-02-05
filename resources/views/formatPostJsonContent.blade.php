@foreach ($json as $content)
    @switch ($content['type'])
        @case (0) {{--文本 {"text": "content\n", "type": "0"} --}}
            <span>{!! str_replace("\n", '<br />', trim($content['text'], "\n")) !!}</span>
            @break
        @case (1)
            {{--链接
                {"link": "http://tieba.baidu.com", "text": "http://tieba.baidu.com/p/", "type": "1"}
                {"link": "http://tieba.baidu.com/mo/q/checkurl?url=", "text": "https://www.google.com", "type": "1"}
                {"link": "http://tieba.baidu.com/mo/q/checkurl?url=", "text": "[失效] http://pan.baidu.com/s/", "type": "1", "url_type": "1"}
                {"link": "http://tieba.baidu.com/mo/q/checkurl?url=", "text": "[有效] http://pan.baidu.com/s/", "type": "1", "url_type": "2"}
            --}}
            <a href="{{ $content['link'] }}" target="_blank">{{ $content['text'] }}</a>
            @break
        @case (2) {{--表情 {"c": "滑稽", "text": "image_emoticon25", "type": "2"} --}}
            <?php
                $emoticonsInfo = [
                    'image_emoticon' => ['class' => 'client', 'type' => 'png'], // 泡泡/客户端新版表情（>=61）
                    //'image_emoticon' => ['class' => 'face', 'prefix' => 'i_f', 'type' => 'gif'], // 旧版泡泡
                    'image_emoticon>51' => ['class' => 'face', 'prefix' => 'i_f', 'type' => 'gif'], // 泡泡-贴吧十周年
                    'bearchildren_' => ['class' => 'bearchildren', 'type' => 'gif'], // 贴吧熊孩子
                    'tiexing_' => ['class' => 'tiexing', 'type' => 'gif'], // 痒小贱
                    'ali_' => ['class' => 'ali', 'type' => 'gif'], // 阿狸
                    'llb_' => ['class' => 'luoluobu', 'type' => 'gif'], // 罗罗布
                    'b' => ['class' => 'qpx_n', 'type' => 'gif'], // 气泡熊
                    'xyj_' => ['class' => 'xyj', 'type' => 'gif'], // 小幺鸡
                    'ltn_' => ['class' => 'lt', 'type' => 'gif'], // 冷兔
                    'bfmn_' => ['class' => 'bfmn', 'type' => 'gif'], // 白发魔女
                    'pczxh_' => ['class' => 'zxh', 'type' => 'gif'], // 张小盒
                    't_' => ['class' => 'tsj', 'type' => 'gif'], // 兔斯基
                    'wdj_' => ['class' => 'wdj', 'type' => 'png'], // 豌豆荚
                    'lxs_' => ['class' => 'lxs', 'type' => 'gif'], // 冷先森
                    'B_' => ['class' => 'bobo', 'type' => 'gif'], // 波波
                    'yz_' => ['class' => 'shadow', 'type' => 'gif'], // 影子
                    'w_' => ['class' => 'ldw', 'type' => 'gif'], // 绿豆蛙
                    '10th_' => ['class' => '10th', 'type' => 'gif'], // 贴吧十周年
                ];
                $emoticonRegex = \Spatie\Regex\Regex::match('/(.+?)(\d+|$)/', $content['text']);
                $emoticonIndex = ['prefix' => $emoticonRegex->group(1), 'index' => $emoticonRegex->group(2) ?? 1]; //TODO : bug
                $emoticonUrlInfo = ($emoticonIndex['prefix'] == 'image_emoticon' && $emoticonIndex['index'] <= 61 && $emoticonIndex['index'] >= 51)
                    ? $emoticonsInfo['image_emoticon>51']
                    : $emoticonsInfo[$emoticonIndex['prefix']];
                $emoticonUrlPrefix = $emoticonUrlInfo['prefix'] ?? $emoticonIndex['prefix'];
                $emoticonUrl = "https://tb2.bdstatic.com/tb/editor/images/{$emoticonUrlInfo['class']}/{$emoticonUrlPrefix}{$emoticonIndex['index']}.{$emoticonUrlInfo['type']}";
            ?>
            <img class="lazyload" data-src="{{ $emoticonUrl }}" alt="{{ $content['c'] }}" />
            @break
        @case (3)
            {{--图片
                {
                    "src": "" // will be filled when displaing copied tieba emoticons
                    "size": "12345", // in byte
                    "type": "3",
                    "bsize": "800,600", // resoultion in pixel
                    "cdn_src": "http://t.hiphotos.baidu.com/forum/w%3D720%3Bq%3D60%3Bg%3D0/sign={unknown token}/{image hash id}.jpg",
                    "origin_src": "http://imgsrc.baidu.com/forum/pic/item/{image hash id}.jpg",
                    "big_cdn_src": "http://t.hiphotos.baidu.com/forum/w%3D960%3Bq%3D60/sign={unknown token}/{image hash id}.jpg",
                    "is_long_pic": "0",
                    "origin_size": "12345", // in byte
                    "cdn_src_active": "http://t.hiphotos.baidu.com/forum/w%3D720%3Bq%3D60%3B/sign={unknown token}/{image hash id}.jpg",
                    "show_original_btn": "0"
                }
                http://imgsrc.baidu.com/forum/abpic/item/{image hash id}.jpg will shown as thumbnail
            --}}
            <img class="d-block lazyload" data-src="{{ str_replace('http://', 'https://', $content['origin_src'] ?? $content['src']) }}" />
            @break
        @case (4) {{-- {"uid": "12345", "text": "(@|)username", "type": "4"} --}}
            <a href="http://tieba.baidu.com/home/main" target="_blank">{{ $content['text'] }}</a>
            @break
        @case (5)
            {{--视频
                external video:
                {
                    "type": "5",
                    "text": "https://www.bilibili.com/video/av12345/"
                }
                tieba video:
                {
                    "src": "http://imgsrc.baidu.com/forum/pic/item/{thumbnail hash id}.jpg", // thumbnail
                    "link": "http://tb-video.bdstatic.com/tieba-smallvideo-transcode/{video hash id}.mp4", // video source
                    "text": "http://tieba.baidu.com/mo/q/movideo/page?thumbnail={thumbnail hash id}&video={video hash id}&product=tieba-movideo&thread_id={tid}", // video play page
                    "type": "5",
                    "bsize": "1024,768", // resoultion of video
                    "count": "0",
                    "width": "1024",
                    "e_type": "15",
                    "height": "768",
                    "native_app": [],
                    "during_time": "123", // in seconds
                    "origin_size": "209767514", // in byte
                    "is_native_app": "0"
                }
            --}}
            <a href="{{ $content['link'] }}" target="_blank">
                @if (isset($content['origin_src']))
                    <img class="d-block lazyload" data-src="{{ str_replace('http://', 'https://', $content['origin_src']) }}" />
                @else
                    外站视频：{{ $content['text'] }}
                @endif
            </a>
            @break
        @case (7) {{--换行 {"type":"7","text":"\n"} not found in many threads --}}
            <br />
            @break
        @case (9) {{--电话 {"text": "12345678 \d{8}", "type": "9", "phonetype": "2"} --}}
            <span>{{ $content['text'] }}</span>
            @break
        @case (10)
            {{--语音
                {
                    "type": "10",
                    "during_time": "123", // in seconds
                    "voice_md5": "{voice hash id}",
                    "is_native_app": "0",
                    "native_app": []
                }
            --}}
            <span>[[语音,时长:{{ $content['during_time'] }}s]]</span>
            @break
        @case (11)
            {{--客户端表情包
                {
                    "c": "", // description
                    "icon": "http://tb2.bdstatic.com/tb/editor/images/faceshop/{packet id}_{packet name}/panel.png",
                    "type": "11",
                    "width": "160",
                    "height": "160",
                    "static": "http://tb2.bdstatic.com/tb/editor/images/faceshop/{packet id}_{packet name}/s_{packet id}_{packet name}_{name}.png",
                    "dynamic": "http://tb2.bdstatic.com/tb/editor/images/faceshop/{packet id}_{packet name}/d_{packet id}_{packet name}_{name}.gif",
                    "packet_name": ""
                }
            --}}
            <img class="d-block lazyload" data-src="{{ str_replace('http://', 'https://', $content['dynamic']) }}" alt="{{ $content['c'] }}" />
            @break
        @case (16)
            {{--涂鸦
                {
                    "type": "16",
                    "bsize": "800,600", // resoultion of image
                    "graffiti_info": {
                        "url": "http://imgsrc.baidu.com/forum/pic/item/{image hash id}.jpg",
                        "gid": "123456" // always 123456
                    },
                    "is_long_pic": "0",
                    "show_original_btn": "0",
                    "cdn_src": "http://t.hiphotos.baidu.com/forum/w%3D720%3Bq%3D60%3Bg%3D0/sign={unknown token}/{image hash id}.jpg",
                    "cdn_src_active": "http://t.hiphotos.baidu.com/forum/w%3D720%3Bq%3D60%3B/sign={unknown token}/{image hash id}.jpg",
                    "big_cdn_src": "http://t.hiphotos.baidu.com/forum/w%3D960%3Bq%3D60/sign={unknown token}/{image hash id}.jpg",
                }
            --}}
            <img class="d-block lazyload" data-src="{{ str_replace('http://', 'https://', $content['graffiti_info']['url']) }}" alt="贴吧涂鸦" />
            @break
        @case (17) {{--活动 not found --}}
            @break
        @case (18) {{--话题 {"link": "http://tieba.baidu.com/mo/q/hotMessage?topic_id={topic id}&fid={forum id}&topic_name={topic name}", "text": "#{topic name}#", "type": "18"} --}}
            <a href="{{ $content['link'] }}" target="_blank">{{ $content['text'] }}</a>
            @break
        @case (20)
            {{--客户端表情商店
                {
                    "src": "http://imgsrc.baidu.com/forum/pic/item/{image hash id}.jpg",
                    "type": "20",
                    "bsize": "800,600",
                    "meme_info": {
                        "width": "800",
                        "height": "600",
                        "pck_id": "0",
                        "pic_id": "{meme id}",
                        "pic_url": "http://imgsrc.baidu.com/forum/pic/item/{image hash id}.jpg",
                        "thumbnail": "http://imgsrc.baidu.com/forum/abpic/item/{image hash id}.jpg",
                        "detail_link": "http://tieba.baidu.com/n/interact/emoticon/0/{meme id}?frompb=1"
                    }
                }
            --}}
            <a href="{{ $content['meme_info']['detail_link'] }}" target="_blank">
                <img class="d-block lazyload" data-src="{{ $content['src'] }}" />
            </a>
            @break
        @default
    @endswitch
@endforeach