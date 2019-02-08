<?php

namespace App\Jobs;

use App\Eloquent\CrawlingPostModel;
use Illuminate\Bus\Queueable;
use Illuminate\Queue\SerializesModels;
use Illuminate\Queue\InteractsWithQueue;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Foundation\Bus\Dispatchable;
use Illuminate\Support\Facades\Log;
use Carbon\Carbon;
use App\Tieba\Crawler;
use App\Tieba\Eloquent;

class ThreadQueue extends CrawlerQueue implements ShouldQueue
{
    use Dispatchable, InteractsWithQueue, Queueable, SerializesModels;

    protected $forumID;

    private $forumName;

    private $queuePushTime;

    public function __construct(int $forumID, string $forumName)
    {
        Log::info("thread queue constructed with {$forumName}");
        $this->forumID = $forumID;
        $this->forumName = $forumName;
        $this->queuePushTime = microtime(true);
    }

    public function handle()
    {
        $queueStartTime = microtime(true);
        Log::info('thread queue start after waiting for ' . ($queueStartTime - $this->queuePushTime));

        \DB::beginTransaction();
        $crawlingForumInfo = ['fid' => $this->forumID, 'tid' => 0];
        $latestCrawlingForum = CrawlingPostModel::select('id', 'startTime')->where($crawlingForumInfo)->lockForUpdate()->first();
        if ($latestCrawlingForum != null) { // is latest crawler existed and started before $queueDeleteAfter ago
            if ($latestCrawlingForum->startTime < new Carbon($this->queueDeleteAfter)) {
                $latestCrawlingForum->delete();
            } else {
                return; // exit queue
            }
        }
        CrawlingPostModel::insert($crawlingForumInfo + ['startTime' => microtime(true)]); // report crawling threads
        \DB::commit();

        $threadsCrawler = (new Crawler\ThreadCrawler($this->forumID, $this->forumName))->doCrawl();
        $newThreadsInfo = $threadsCrawler->getThreadsInfo();
        $oldThreadsInfo = self::convertIDListKey(
            Eloquent\PostModelFactory::newThread($this->forumID)
                ->select('tid', 'latestReplyTime', 'replyNum')
                ->whereIn('tid', array_keys($newThreadsInfo))->get()->toArray(),
            'tid'
        );
        $threadsCrawler->saveLists();

        \DB::transaction(function () use ($newThreadsInfo, $oldThreadsInfo) {
            $latestCrawlingThreads = CrawlingPostModel::select('id', 'tid', 'startTime')
                ->whereIn('tid', array_keys($newThreadsInfo))->lockForUpdate()->get();
            $latestCrawlingThreadsID = array_column($latestCrawlingThreads->toArray(), 'tid');
            foreach ($newThreadsInfo as $tid => $newThread) {
                if (array_search($tid, $latestCrawlingThreadsID) === true) { // is latest crawler existed and started before $queueDeleteAfter ago
                    if ($latestCrawlingThreads->startTime < new Carbon($this->queueDeleteAfter)) {
                        $latestCrawlingThreads->delete();
                    } else {
                        continue;
                    }
                }
                if ((! isset($oldThreadsInfo[$tid]))
                    || (strtotime($newThread['latestReplyTime']) != strtotime($oldThreadsInfo[$tid]['latestReplyTime']))
                    || ($newThread['replyNum'] != $oldThreadsInfo[$tid]['replyNum'])) {
                    CrawlingPostModel::insert([
                        'fid' => $this->forumID,
                        'tid' => $tid,
                        'startTime' => microtime(true)
                    ]); // report crawling replies
                    ReplyQueue::dispatch($this->forumID, $tid);
                }
            }
        });

        $queueFinishTime = microtime(true);
        \DB::transaction(function () use ($queueFinishTime) {
            // report previous finished forum crawl
            $previousCrawlingForum = CrawlingPostModel::select('id', 'startTime')->where(['fid' => $this->forumID, 'tid' => 0])->first();
            if ($previousCrawlingForum != null) { // might already marked as finished by other concurrency queues
                $previousCrawlingForum->fill(['duration' => $queueFinishTime - $previousCrawlingForum->startTime])->save();
                $previousCrawlingForum->delete();
            }
        });
        Log::info('thread queue handled after ' . ($queueFinishTime - $queueStartTime));
    }
}
