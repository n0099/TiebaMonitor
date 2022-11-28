using static tbm.Crawler.MainCrawlWorker;

namespace tbm.Crawler
{
    public class RetryCrawlWorker : CyclicCrawlWorker
    {
        private readonly ILogger<RetryCrawlWorker> _logger;
        private readonly ILifetimeScope _scope0;
        private readonly IIndex<string, CrawlerLocks> _registeredLocksFactory;

        public RetryCrawlWorker(ILogger<RetryCrawlWorker> logger, IConfiguration config,
            ILifetimeScope scope0, IIndex<string, CrawlerLocks> registeredLocksFactory) : base(config)
        {
            _logger = logger;
            _scope0 = scope0;
            _registeredLocksFactory = registeredLocksFactory;
            _ = SyncCrawlIntervalWithConfig();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Timer.Elapsed += async (_, _) => await Retry();
            await Retry();
        }

        private async Task Retry()
        {
            _ = SyncCrawlIntervalWithConfig();
            try
            {
                foreach (var lockType in Program.RegisteredCrawlerLocks)
                {
                    var failed = _registeredLocksFactory[lockType].RetryAllFailed();
                    if (!failed.Any()) continue; // skip current lock type if there's nothing needs to retry
                    if (lockType == "threadLate")
                    {
                        await using var scope1 = _scope0.BeginLifetimeScope();
                        var db = scope1.Resolve<TbmDbContext.New>()(0);
                        foreach (var tidGroupByFid in failed.Keys.GroupBy(i => i.Fid, i => i.Tid))
                        {
                            var fid = tidGroupByFid.Key;
                            FailedCount FailedCountSelector(Tid tid) => failed[new (fid, tid)].First().Value; // it should always contains only one page which is 1
                            var failedCountsKeyByTid = tidGroupByFid.Cast<Tid>().ToDictionary(tid => tid, FailedCountSelector);
                            _logger.LogTrace("Retrying previous failed thread late crawl with fid:{}, threadsId:{}",
                                fid, Helper.UnescapedJsonSerialize(tidGroupByFid));
                            await scope1.Resolve<ThreadLateCrawlerAndSaver.New>()(fid).Crawl(failedCountsKeyByTid);
                        }
                        continue; // skip into next lock type
                    }

                    await Task.WhenAll(failed.Select(async pair =>
                    {
                        await using var scope1 = _scope0.BeginLifetimeScope();
                        var db = scope1.Resolve<TbmDbContext.New>()(0);
                        var (lockId, failedCountKeyByPage) = pair;
                        var pages = failedCountKeyByPage.Keys;
                        FailedCount FailedCountSelector(Page p) => failedCountKeyByPage[p];

                        if (lockType == "thread")
                        {
                            var fid = lockId.Fid;
                            var forumName = (from f in db.ForumsInfo where f.Fid == fid select f.Name).FirstOrDefault();
                            if (forumName == null) return;
                            _logger.LogTrace("Retrying previous failed {} pages in thread crawl for fid:{}, forumName:{}", failedCountKeyByPage.Count, fid, forumName);
                            var crawler = scope1.Resolve<ThreadCrawlFacade.New>()(fid, forumName);
                            var savedThreads = await crawler.RetryThenSave(pages, FailedCountSelector);
                            if (savedThreads == null) return;
                            await CrawlSubReplies(await CrawlReplies(new() {savedThreads}, fid, scope1), fid, scope1);
                        }
                        else if (lockType == "reply" && lockId.Tid != null)
                        {
                            _logger.LogTrace("Retrying previous failed {} pages reply crawl for fid:{}, tid:{}", failedCountKeyByPage.Count, lockId.Fid, lockId.Tid);
                            var crawler = scope1.Resolve<ReplyCrawlFacade.New>()(lockId.Fid, lockId.Tid.Value);
                            var savedReplies = await crawler.RetryThenSave(pages, FailedCountSelector);
                            if (savedReplies == null) return;
                            await CrawlSubReplies(new Dictionary<ulong, SaverChangeSet<ReplyPost>> {{lockId.Tid.Value, savedReplies}}, lockId.Fid, scope1);
                        }
                        else if (lockType == "subReply" && lockId.Tid != null && lockId.Pid != null)
                        {
                            _logger.LogTrace("Retrying previous failed {} pages sub reply crawl for fid:{}, tid:{}, pid:{}", failedCountKeyByPage.Count, lockId.Fid, lockId.Tid, lockId.Pid);
                            var crawler = scope1.Resolve<SubReplyCrawlFacade.New>()(lockId.Fid, lockId.Tid.Value, lockId.Pid.Value);
                            _ = await crawler.RetryThenSave(pages, FailedCountSelector);
                        }
                    }));
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception");
            }
        }
    }
}
