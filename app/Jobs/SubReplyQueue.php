<?php

namespace App\Jobs;

use App\Eloquent\CrawlingPostModel;
use Illuminate\Bus\Queueable;
use Illuminate\Queue\SerializesModels;
use Illuminate\Queue\InteractsWithQueue;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Foundation\Bus\Dispatchable;
use Illuminate\Support\Facades\Log;
use App\Tieba\Crawler;

class SubReplyQueue extends CrawlerQueue implements ShouldQueue
{
    use Dispatchable, InteractsWithQueue, Queueable, SerializesModels;

    private $forumId;

    private $threadId;

    private $replyId;

    private $queuePushTime;

    public function __construct(int $fid, int $tid, int $pid)
    {
        Log::info('sub reply queue constructed with' . "{$tid} in forum {$fid}");

        $this->forumId = $fid;
        $this->threadId = $tid;
        $this->replyId = $pid;
        $this->queuePushTime = microtime(true);
    }

    public function handle()
    {
        $queueStartTime = microtime(true);
        Log::info('sub reply queue start after waiting for ' . ($queueStartTime - $this->queuePushTime));

        (new Crawler\SubReplyCrawler($this->forumId, $this->threadId, $this->replyId))->doCrawl()->saveLists();
        echo 'subreply:' . memory_get_usage() . PHP_EOL;

        $queueFinishTime = microtime(true);
        // report finished sub reply crawl
        $currentCrawlingSubReply = CrawlingPostModel::select('id', 'startTime')->where(['tid' => $this->threadId, 'pid' => $this->replyId])->first();
        $currentCrawlingSubReply->fill(['duration' => $queueFinishTime - $currentCrawlingSubReply->startTime])->save();
        $currentCrawlingSubReply->delete();
        Log::info('sub reply queue handled after ' . ($queueFinishTime - $queueStartTime));
    }
}