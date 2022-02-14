using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Autofac.Features.Indexed;
using LinqKit;
using Microsoft.Extensions.Logging;
using static System.Text.Json.JsonElement;
using Page = System.UInt32;
using Fid = System.UInt32;
using Tid = System.UInt64;
using Pid = System.UInt64;
using Uid = System.Int64;
using Time = System.UInt32;

namespace tbm.Crawler
{
    public sealed class ThreadCrawler : BaseCrawler<ThreadPost>
    {
        private readonly string _forumName;

        public delegate ThreadCrawler New(Fid fid, string forumName);

        public ThreadCrawler(
            ILogger<ThreadCrawler> logger,
            ClientRequester requester,
            ClientRequesterTcs requesterTcs,
            UserParser userParser,
            IIndex<string, CrawlerLocks.New> locks,
            Fid fid,
            string forumName
        ) : base(logger, requester, requesterTcs, userParser, (locks["thread"]("thread"), fid), fid) =>
            _forumName = forumName;

        protected override Exception FillExceptionData(Exception e)
        {
            e.Data["forumName"] = _forumName;
            return e;
        }

        protected override async Task<JsonElement> CrawlSinglePage(Page page) =>
            await RequestJson("http://c.tieba.baidu.com/c/f/frs/page", new Dictionary<string, string>
            {
                {"kw", _forumName},
                {"pn", page.ToString()},
                {"rn", "50"}
            });

        protected override ArrayEnumerator GetValidPosts(JsonElement json)
        {
            ValidateOtherErrorCode(json);
            return EnsureNonEmptyPostList(json, "thread_list",
                "Forum threads list is empty, forum might doesn't existed");
        }

        protected override void ParsePosts(ArrayEnumerator posts)
        {
            List<JsonElement> users = new();
            var newPosts = posts.Select(p =>
            {
                var author = p.GetProperty("author");
                users.Add(author);
                var post = new ThreadPost();
                try
                {
                    post.Tid = Tid.Parse(p.GetStrProp("tid"));
                    post.FirstPid = Pid.Parse(p.GetStrProp("first_post_id"));
                    post.ThreadType = ulong.Parse(p.GetStrProp("thread_types"));
                    post.StickyType = p.GetStrProp("is_membertop") == "1"
                        ? "membertop"
                        : p.TryGetProperty("is_top", out var isTop)
                            ? isTop.GetString() == "0" ? null : "top"
                            : "top"; // in 6.0.2 client version, if there's a vip sticky thread and three normal sticky threads, the fourth (oldest) thread won't have is_top field
                    post.IsGood = p.GetStrProp("is_good") == "1";
                    post.TopicType = p.TryGetProperty("is_livepost", out var isLivePost)
                        ? isLivePost.GetString()
                        : null;
                    post.Title = p.GetStrProp("title");
                    post.AuthorUid = Uid.Parse(author.GetStrProp("id"));
                    post.AuthorManagerType = author.TryGetProperty("bawu_type", out var bawuType)
                        ? bawuType.GetString().NullIfWhiteSpace()
                        : null; // topic thread won't have this
                    post.PostTime = p.TryGetProperty("create_time", out var createTime)
                        ? Time.Parse(createTime.GetString() ?? "")
                        : null; // topic thread won't have this
                    post.LatestReplyTime = Time.Parse(p.GetStrProp("last_time_int"));
                    post.LatestReplierUid = Uid.Parse(p.GetProperty("last_replyer").GetStrProp("id")); // topic thread won't have this
                    post.ReplyNum = uint.Parse(p.GetStrProp("reply_num"));
                    post.ViewNum = uint.Parse(p.GetStrProp("view_num"));
                    post.ShareNum = p.TryGetProperty("share_num", out var shareNum)
                        ? uint.Parse(shareNum.GetString() ?? "")
                        : null; // topic thread won't have this
                    post.AgreeNum = int.Parse(p.GetStrProp("agree_num"));
                    post.DisagreeNum = int.Parse(p.GetStrProp("disagree_num"));
                    post.Location = RawJsonOrNullWhenEmpty(p.GetProperty("location"));
                    post.ZanInfo = RawJsonOrNullWhenEmpty(p.GetProperty("zan"));
                    return post;
                }
                catch (Exception e)
                {
                    e.Data["rawJson"] = p.GetRawText();
                    throw new Exception("Thread parse error", e);
                }
            });
            newPosts.ForEach(i => Posts[i.Tid] = i);
            Users.ParseUsers(users);
        }

        protected override void SavePosts(TbmDbContext db)
        {
            SavePosts(db,
                PredicateBuilder.New<ThreadPost>(p => Posts.Keys.Any(id => id == p.Tid)),
                PredicateBuilder.New<PostIndex>(i => i.Type == "thread" && Posts.Keys.Any(id => id == i.Tid)),
                p => p.Tid,
                i => i.Tid,
                p => new PostIndex {Type = "thread", Fid = Fid, Tid = p.Tid, PostTime = p.PostTime},
                (now, p) => new ThreadRevision {Time = now, Tid = p.Tid});
            foreach (var post in db.Set<ThreadPost>().Local)
            { // prevent update with default null value on fields which will be later set by ReplyCrawler
                db.Entry(post).Properties
                    .Where(p => p.Metadata.Name is nameof(ThreadLateSaveInfo.AntiSpamInfo) or nameof(ThreadLateSaveInfo.AuthorPhoneType))
                    .ForEach(p => p.IsModified = false);
            }
        }
    }
}
