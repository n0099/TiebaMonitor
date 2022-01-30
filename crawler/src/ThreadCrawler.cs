using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Autofac.Features.Indexed;
using Microsoft.Extensions.Logging;
using static System.Text.Json.JsonElement;
using Page = System.UInt32;
using Fid = System.UInt32;
using Tid = System.UInt64;
using Pid = System.UInt64;
using Uid = System.Int64;
using Time = System.UInt32;

namespace tbm
{
    public sealed class ThreadCrawler : BaseCrawler
    {
        protected override CrawlerLocks CrawlerLocks { get; init; }
        private readonly string _forumName;

        public delegate ThreadCrawler New(Fid fid, string forumName);

        public ThreadCrawler(
            ILogger<ThreadCrawler> logger,
            ClientRequester requester,
            ClientRequesterTcs requesterTcs,
            IIndex<string, CrawlerLocks.New> locks,
            Fid fid,
            string forumName
        ) : base(logger, requesterTcs, requester, fid)
        {
            _forumName = forumName;
            CrawlerLocks = locks["thread"]("thread");
        }

        protected override void ParseThreads(ArrayEnumerator threads)
        {
            static string? NullIfEmptyJsonLiteral(string json) => json is @"""""" or "[]" ? null : json;
            var newThreads = threads.Select(t => new ThreadPost
            {
                Tid = Tid.Parse(t.GetStrProp("tid")),
                FirstPid = Pid.Parse(t.GetStrProp("first_post_id")),
                ThreadType = ulong.Parse(t.GetStrProp("thread_types")),
                StickyType = t.GetStrProp("is_membertop") == "1"
                    ? "membertop"
                    : t.TryGetProperty("is_top", out var isTop)
                        ? isTop.GetString() == "0" ? null : "top"
                        : "top", // in 6.0.2 client version, if there's a vip sticky thread and three normal sticky threads, the fourth (oldest) thread won't have is_top field
                IsGood = t.GetStrProp("is_good") == "1",
                TopicType = t.TryGetProperty("is_livepost", out var isLivePost) ? isLivePost.GetString() : null,
                Title = t.GetStrProp("title"),
                AuthorUid = Uid.Parse(t.GetProperty("author").GetStrProp("id")),
                AuthorManagerType = t.GetProperty("author").TryGetProperty("bawu_type", out var bawuType) ? bawuType.GetString().NullIfWhiteSpace() : null, // topic thread won't have this
                PostTime = t.TryGetProperty("create_time", out var createTime) ? Time.Parse(createTime.GetString() ?? "") : null, // topic thread won't have this
                LatestReplyTime = Time.Parse(t.GetStrProp("last_time_int")),
                LatestReplierUid = Uid.Parse(t.GetProperty("last_replyer").GetStrProp("id")), // topic thread won't have this
                ReplyNum = uint.Parse(t.GetStrProp("reply_num")),
                ViewNum = uint.Parse(t.GetStrProp("view_num")),
                ShareNum = t.TryGetProperty("share_num", out var shareNum) ? uint.Parse(shareNum.GetString() ?? "") : null, // topic thread won't have this
                AgreeNum = int.Parse(t.GetStrProp("agree_num")),
                DisagreeNum = int.Parse(t.GetStrProp("disagree_num")),
                Location = NullIfEmptyJsonLiteral(t.GetProperty("location").GetRawText()),
                ZanInfo = NullIfEmptyJsonLiteral(t.GetProperty("zan").GetRawText())
            });
            newThreads.ToList().ForEach(i => Posts[i.Tid] = i); // newThreads will overwrite Posts with same tid
        }

        protected override ArrayEnumerator ValidateJson(JsonElement json)
        {
            ValidateOtherErrorCode(json);
            return CheckIsEmptyPostList(json, "thread_list",
                "Forum threads list is empty, forum might doesn't existed");
        }

        protected override async Task<JsonElement> CrawlSinglePage(Page page) =>
            await RequestJson("http://c.tieba.baidu.com/c/f/frs/page", new Dictionary<string, string>
            {
                {"kw", _forumName},
                {"pn", page.ToString()},
                {"rn", "50"}
            });

        protected override Exception FillExceptionData(Exception e)
        {
            e.Data["forumName"] = _forumName;
            return e;
        }
    }
}