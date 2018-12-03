<?php

namespace App\Tieba\Crawler;

use App\Tieba\Eloquent;
use Carbon\Carbon;
use GuzzleHttp;
use function GuzzleHttp\json_decode;
use function GuzzleHttp\json_encode;
use Illuminate\Support\Facades\Log;

class ReplyCrawler extends Crawlable
{
    protected $clientVersion = '8.8.8';

    protected $threadId;

    protected $repliesList = [];

    protected $indexesList = [];

    protected $repliesUpdateInfo = [];

    public function doCrawl(): self
    {
        $client = $this->getClientHelper();

        Log::info("Start to fetch replies for tid {$this->threadId}, page 1");
        $repliesJson = json_decode($client->post(
            'http://c.tieba.baidu.com/c/f/pb/page',
            ['form_params' => ['kz' => $this->threadId, 'pn' => 1]] // reverse order = ['last'=>1,'r'=>1]
        )->getBody(), true);

        $this->parseRepliesList($repliesJson);

        (new GuzzleHttp\Pool(
            $client,
            (function () use ($client, $repliesJson) {
                for ($pn = 2; $pn <= $repliesJson['page']['total_page']; $pn++) {
                    yield function () use ($client, $pn) {
                        Log::info("Start to fetch replies for tid {$this->threadId}, page {$pn}");
                        return $client->postAsync(
                            'http://c.tieba.baidu.com/c/f/pb/page',
                            ['form_params' => ['kz' => $this->threadId, 'pn' => $pn]]
                        );
                    };
                }
            })(),
            [
                'concurrency' => 10,
                'fulfilled' => function (\Psr\Http\Message\ResponseInterface $response, int $index) {
                    //add_measure($response->getReasonPhrase(), microtime(true), microtime(true));
                    $repliesJson = json_decode($response->getBody(), true);
                    $this->parseRepliesList($repliesJson);
                },
                'rejected' => function (GuzzleHttp\Exception\RequestException $e, int $index) {
                    report($e);
                }
            ]
        ))->promise()->wait();

        return $this;
    }

    private static function convertUsersListToUidKey(array $usersList): array
    {
        $newUsersList = [];

        foreach ($usersList as $user) {
            $uid = $user['id'];
            $newUsersList[$uid] = $user;
        }

        return $newUsersList;
    }

    private function parseRepliesList(array $repliesJson): void
    {
        if ($repliesJson['error_code'] == 0) {
            $repliesList = $repliesJson['post_list'];
            $usersList = $repliesJson['user_list'];
        } else {
            throw new \RuntimeException("Error from tieba client, raw json: " . json_encode($repliesJson));
        }
        if (count($repliesList) == 0) {
            throw new \LengthException('Reply posts list is empty');
        }

        $usersList = self::convertUsersListToUidKey($usersList);
        $repliesUpdateInfo = [];
        $repliesInfo = [];
        $indexesInfo = [];
        $now = Carbon::now();
        foreach ($repliesList as $reply) {
            $repliesInfo[] = [
                'tid' => $this->threadId,
                'pid' => $reply['id'],
                'floor' => $reply['floor'],
                'content' => self::valueValidate($reply['content'], true),
                'authorUid' => $reply['author_id'],
                'authorManagerType' => self::valueValidate($usersList[$reply['author_id']]['bawu_type']),
                'authorExpGrade' => $usersList[$reply['author_id']]['level_id'],
                'subReplyNum' => $reply['sub_post_number'],
                'replyTime' => Carbon::createFromTimestamp($reply['time'])->toDateTimeString(),
                'isFold' => $reply['is_fold'],
                'agreeInfo' => self::valueValidate(($reply['agree']['has_agree'] > 0 ? $reply['agree'] : null), true),
                'signInfo' => self::valueValidate($reply['signature'], true),
                'tailInfo' => self::valueValidate($reply['tail_info'], true),
                'clientVersion' => $this->clientVersion,
                'created_at' => $now,
                'updated_at' => $now
            ];

            $latestInfo = end($repliesInfo);
            if ($reply['sub_post_number'] > 0) {
                $repliesUpdateInfo[$reply['id']] = self::getSubKeyValueByKeys($latestInfo, ['subReplyNum']);
            }
            $indexesInfo[] = [
                'created_at' => $now,
                'updated_at' => $now,
                'postTime' => $latestInfo['replyTime'],
                'type' => 'reply',
                'fid' => $this->forumId
            ] + self::getSubKeyValueByKeys($latestInfo, ['tid', 'pid', 'authorUid']);
        }

        // Lazy saving to Eloquent model
        $this->parseUsersList($usersList);
        $this->repliesUpdateInfo = $repliesUpdateInfo + $this->repliesUpdateInfo;
        $this->repliesList = array_merge($this->repliesList, $repliesInfo);
        $this->indexesList = array_merge($this->indexesList, $indexesInfo);
    }

    public function saveLists(): self
    {
        $updateExceptFields = array_diff(array_keys($this->repliesList[0]), [
            'tid',
            'pid',
            'floor',
            'replyTime',
            'authorUid',
            'created_at'
        ]);
        // TODO: performance issue on big query
        Eloquent\PostModelFactory::newReply($this->forumId)->insertOnDuplicateKey($this->repliesList, $updateExceptFields);
        $indexExceptFields = array_diff(array_keys($this->indexesList[0]), ['created_at']);
        (new \App\Eloquent\IndexModel())->insertOnDuplicateKey($this->indexesList, $indexExceptFields);
        $this->saveUsersList();

        return $this;
    }

    public function getRepliesInfo(): array
    {
        return $this->repliesUpdateInfo;
    }

    public function __construct(int $fid, int $tid)
    {
        $this->forumId = $fid;
        $this->threadId = $tid;
    }
}