<?php

namespace App\Jobs\Crawler;

use App\Eloquent\CrawlingPostModel;
use App\Helper;
use App\Tieba\Crawler\ThreadCrawler;
use App\Tieba\Eloquent\PostModelFactory;
use Carbon\Carbon;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Foundation\Bus\Dispatchable;
use Illuminate\Queue\InteractsWithQueue;
use Illuminate\Queue\SerializesModels;
use Illuminate\Support\Facades\Log;

class ThreadQueue extends CrawlerQueue implements ShouldQueue
{
    use Dispatchable, InteractsWithQueue, Queueable, SerializesModels;

    protected $queueStartTime;

    protected $forumID;

    protected $forumName;

    public function __construct(int $forumID, string $forumName)
    {
        Log::info("Thread crawler queue dispatched with {$forumID}({$forumName})");
        $this->forumID = $forumID;
        $this->forumName = $forumName;
    }

    public function handle()
    {
        $this->queueStartTime = microtime(true);
        \DB::statement('SET SESSION TRANSACTION ISOLATION LEVEL READ COMMITTED'); // change present crawler queue session's transaction isolation level to reduce deadlock

        $cancelCurrentCrawler = false;
        \DB::transaction(function () use ($cancelCurrentCrawler) {
            $crawlingForumInfo = [
                'type' => 'thread',
                'fid' => $this->forumID,
            ];
            $latestCrawlingForum = CrawlingPostModel
                ::select('id', 'startTime')
                ->where('type', 'thread') // not including reply and sub reply crawler queue
                ->where($crawlingForumInfo)
                ->lockForUpdate()->first();
            if ($latestCrawlingForum != null) { // is latest crawler existed and started before $queueDeleteAfter ago
                if ($latestCrawlingForum->startTime < new Carbon($this->queueDeleteAfter)) {
                    $latestCrawlingForum->delete(); // release latest parallel thread crawler lock then dispatch new one when it's has started before $queueDeleteAfter ago
                } else {
                    $cancelCurrentCrawler = true; // cancel pending thread crawl because it's already crawling
                }
            }
            if (! $cancelCurrentCrawler) {
                CrawlingPostModel::insert($crawlingForumInfo + [
                    'startTime' => microtime(true)
                ]); // lock for current pending thread crawler
            }
        });
        if ($cancelCurrentCrawler) {
            return;
        }

        $threadsCrawler = (new ThreadCrawler($this->forumName, $this->forumID, 1))->doCrawl();
        $newThreadsInfo = $threadsCrawler->getThreadsInfo();
        $oldThreadsInfo = Helper::convertIDListKey(
            PostModelFactory::newThread($this->forumID)
                ->select('tid', 'latestReplyTime', 'replyNum')
                ->whereIn('tid', array_keys($newThreadsInfo))->get()->toArray(),
            'tid'
        );
        ksort($oldThreadsInfo);
        $threadsCrawler->saveLists();

        \DB::transaction(function () use ($newThreadsInfo, $oldThreadsInfo) {
            $parallelCrawlingReplies = CrawlingPostModel
                ::select('id', 'tid', 'startTime')
                ->whereIn('type', ['reply', 'subReply']) // including sub reply to prevent repeat crawling sub reply's parent reply (optional optimize case)
                ->whereIn('tid', array_keys($newThreadsInfo))
                ->lockForUpdate()->get();
            foreach ($newThreadsInfo as $tid => $newThread) {
                // check for other parallelling reply crawler lock
                foreach ($parallelCrawlingReplies as $parallelCrawlingReply) {
                    if ($parallelCrawlingReply->tid == $tid) {
                        if ($parallelCrawlingReply->startTime < new Carbon($this->queueDeleteAfter)) {
                            $parallelCrawlingReply->delete(); // release latest parallel reply crawler lock then dispatch new crawler when it's has started before $queueDeleteAfter ago
                        } else {
                            continue 2; // cancel pending thread's reply crawl because it's already crawling
                        }
                    }
                }
                if (! isset($oldThreadsInfo[$tid]) // do we have to crawl new replies under thread
                    || (strtotime($newThread['latestReplyTime']) != strtotime($oldThreadsInfo[$tid]['latestReplyTime']))
                    || ($newThread['replyNum'] != $oldThreadsInfo[$tid]['replyNum'])) {
                    $firstReplyCrawlPage = 1;
                    CrawlingPostModel::insert([
                        'type' => 'reply',
                        'fid' => $this->forumID,
                        'tid' => $tid,
                        'startPage' => $firstReplyCrawlPage,
                        'startTime' => microtime(true)
                    ]); // lock for current thread's reply crawler
                    ReplyQueue::dispatch($this->forumID, $tid, $firstReplyCrawlPage)->onQueue('crawler');
                }
            }
        });

        $queueFinishTime = microtime(true);
        \DB::transaction(function () use ($queueFinishTime, $threadsCrawler) {
            // report previous finished forum crawl
            $currentCrawlingForum = CrawlingPostModel
                ::select('id', 'startTime')
                ->where([
                    'type' => 'thread', // not including current thread's reply and sub reply crawler queue
                    'fid' => $this->forumID,
                ])
                ->lockForUpdate()->first();
            if ($currentCrawlingForum != null) { // might already marked as finished by other concurrency queues
                $currentCrawlingForum->fill([
                    'duration' => $queueFinishTime - $this->queueStartTime
                ] + $threadsCrawler->getProfiles())->save();
                $currentCrawlingForum->delete(); // release current crawl queue lock
            }
        });
        Log::info('Thread crawler queue completed after ' . ($queueFinishTime - $this->queueStartTime));
    }
}
