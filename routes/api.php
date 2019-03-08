<?php

use App\Tieba\Eloquent\PostModelFactory;
use Illuminate\Http\Request;

/*
|--------------------------------------------------------------------------
| API Routes
|--------------------------------------------------------------------------
|
| Here is where you can register API routes for your application. These
| routes are loaded by the RouteServiceProvider within a group which
| is assigned the "api" middleware group. Enjoy building your API!
|
*/

Route::get('/postsQuery', 'PostsQueryController@query');
Route::get('/forumsList', function () {
    echo App\Tieba\Eloquent\ForumModel::all()->toJson();
});
Route::get('/status', function () {
    return \DB::query()
        ->select('startTime')
        ->selectRaw('SUM(duration) AS duration')
        ->selectRaw('SUM(webRequestTimes) AS webRequestTimes')
        ->selectRaw('SUM(parsedPostTimes) AS parsedPostTimes')
        ->selectRaw('SUM(parsedUserTimes) AS parsedUserTimes')
        ->fromSub(function ($query) {
            $query->from('tbm_crawledPosts')->selectRaw('FROM_UNIXTIME(startTime, "%Y-%m-%dT%H:%i") AS startTime')->addSelect([
                'duration',
                'webRequestTimes',
                'parsedPostTimes',
                'parsedUserTimes'
            ])->orderBy('id', 'DESC')->limit(10000);
        }, 'T')
        ->groupBy('startTime')
        ->get()->toJson();
});
Route::get('/stats/forumPostsCount', function () {
    $queryParams = collect(\Request::query());
    $queryParams->shift();
    $requiredParams = ['fid', 'timeRange', 'startTime', 'endTime'];
    sort($requiredParams);
    abort_if($queryParams->keys()->sort()->diff($requiredParams)->isEmpty(), 400, 'Missing required query params');

    $groupTimeRangeRawSQL = [
        'minute' => 'DATE_FORMAT(postTime, "%Y-%m-%d %H:%i") AS time',
        'hour' => 'DATE_FORMAT(postTime, "%Y-%m-%d %H:00") AS time',
        'day' => 'DATE(postTime) AS time',
        'week' => 'DATE_FORMAT(postTime, "%Y第%u周") AS time',
        'month' => 'DATE_FORMAT(postTime, "%Y-%m") AS time',
        'year' => 'DATE_FORMAT(postTime, "%Y") AS time',
    ];
    $forumPostsCount = [];
    foreach (PostModelFactory::getPostsModelByForumID($queryParams['fid']) as $postType => $forumPostModel) {
        $forumPostsCount[$postType] = $forumPostModel
            ->selectRaw($groupTimeRangeRawSQL[$queryParams['timeRange']])
            ->selectRaw('COUNT(*) AS count')
            ->whereBetween('postTime', [$queryParams['startTime'], $queryParams['endTime']])
            ->groupBy('time')
            ->get()->toArray();
    }

    return json_encode($forumPostsCount);
});

Route::middleware('auth:api')->get('/user', function (Request $request) {
    return $request->user();
});
