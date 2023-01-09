using AngleSharp;
using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace tbm.Crawler.Worker
{
    public class ForumModeratorRevisionCrawlWorker : CyclicCrawlWorker
    {
        private readonly ILifetimeScope _scope0;

        public ForumModeratorRevisionCrawlWorker(ILogger<ForumModeratorRevisionCrawlWorker> logger,
            IConfiguration config, ILifetimeScope scope0) : base(logger, config, false) => _scope0 = scope0;

        protected override async Task DoWork(CancellationToken stoppingToken)
        {
            await using var scope1 = _scope0.BeginLifetimeScope();
            var db0 = scope1.Resolve<TbmDbContext.New>()(0);
            var browsing = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
            foreach (var forum in from f in db0.Forum.AsNoTracking() where f.IsCrawling select new {f.Fid, f.Name})
            {
                if (stoppingToken.IsCancellationRequested) return;
                var doc = await browsing.OpenAsync($"https://tieba.baidu.com/bawu2/platform/listBawuTeamInfo?ie=utf-8&word={forum.Name}", stoppingToken);
                var moderators = doc.QuerySelectorAll("div.bawu_single_type").Select(typeEl =>
                {
                    var type = typeEl.QuerySelector("div.title")?.Children
                        .Select(el => el.ClassList)
                        .First(classNames => classNames.Any(className => className.EndsWith("_icon")))
                        .Select(className => className.Split("_")[0])
                        .First(className => !string.IsNullOrWhiteSpace(className));
                    if (string.IsNullOrEmpty(type)) throw new TiebaException();
                    var memberPortraits = typeEl.QuerySelectorAll(".member")
                        .Select(memberEl => memberEl.QuerySelector("a.avatar")
                            ?.GetAttribute("href")?.Split("/home/main?id=")[1].NullIfWhiteSpace())
                        .OfType<string>();
                    return memberPortraits.Select(portrait => (type, portrait));
                });

                var fid = forum.Fid;
                var now = (Time)DateTimeOffset.Now.ToUnixTimeSeconds();
                await using var db1 = scope1.Resolve<TbmDbContext.New>()(0);
                await using var transaction = await db1.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, stoppingToken);
                var revisions = moderators.SelectMany(i => i).Select(tuple => new ForumModeratorRevision
                {
                    Time = now,
                    Fid = fid,
                    Portrait = tuple.portrait,
                    ModeratorType = tuple.type
                }).ToList();
                var existingLatestRevisions = (from r in db1.ForumModeratorRevisions.AsNoTracking()
                    where r.Fid == fid
                    select new
                    {
                        r.Time,
                        r.Portrait,
                        r.ModeratorType,
                        Rank = Sql.Ext.Rank().Over().PartitionBy(r.Portrait).OrderByDesc(r.Time).ToValue()
                    }).Where(e => e.Rank == 1).ToLinqToDB().ToList();

                db1.ForumModeratorRevisions.AddRange(revisions.ExceptBy(
                    existingLatestRevisions.Select(e => (e.Portrait, e.ModeratorType)),
                    r => (r.Portrait, r.ModeratorType)));
                db1.ForumModeratorRevisions.AddRange(existingLatestRevisions.ExceptBy(
                    revisions.Select(r => (r.Portrait, r.ModeratorType)),
                    e => (e.Portrait, e.ModeratorType))
                    .Select(e => new ForumModeratorRevision
                    {
                        Time = now,
                        Fid = fid,
                        Portrait = e.Portrait,
                        ModeratorType = null // moderator only exists in db means he is no longer a moderator
                    }));
                _ = await db1.SaveChangesAsync(stoppingToken);
                await transaction.CommitAsync(stoppingToken);
            }
        }
    }
}
