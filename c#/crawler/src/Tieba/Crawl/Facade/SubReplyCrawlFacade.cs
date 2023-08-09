namespace tbm.Crawler.Tieba.Crawl.Facade;

public class SubReplyCrawlFacade(
        ILogger<SubReplyCrawlFacade> logger,
        CrawlerDbContext.New dbContextFactory,
        SubReplyCrawler.New crawler,
        SubReplyParser parser,
        SubReplySaver.New saver,
        UserParserAndSaver users,
        SonicPusher pusher,
        ClientRequesterTcs requesterTcs,
        IIndex<string, CrawlerLocks> locks,
        Fid fid,
        Tid tid,
        Pid pid)
    : BaseCrawlFacade<SubReplyPost, BaseSubReplyRevision, SubReplyResponse, SubReply>(logger, dbContextFactory, crawler(tid, pid), parser, saver.Invoke, users,
    requesterTcs, (locks["subReply"], new(fid, tid, pid)), fid)
{
    public delegate SubReplyCrawlFacade New(Fid fid, Tid tid, Pid pid);

    protected override void ThrowIfEmptyUsersEmbedInPosts() => throw new TiebaException(
        $"User list in the response of sub reply request for fid {Fid}, tid {tid}, pid {pid} is empty.");

    protected override void PostParseHook(SubReplyResponse response, CrawlRequestFlag flag, Dictionary<PostId, SubReplyPost> parsedPostsInResponse)
    {
        foreach (var sr in parsedPostsInResponse.Values)
        {
            sr.Tid = tid;
            sr.Pid = pid;
        }
        Users.ResetUsersIcon();
    }

    protected override void PostCommitSaveHook(SaverChangeSet<SubReplyPost> savedPosts, CancellationToken stoppingToken = default) =>
        pusher.PushPostWithCancellationToken(savedPosts.NewlyAdded, Fid, "subReplies",
            p => p.Spid, p => p.OriginalContents, stoppingToken);
}
