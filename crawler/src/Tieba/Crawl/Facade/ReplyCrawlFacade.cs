namespace tbm.Crawler.Tieba.Crawl.Facade
{
    public class ReplyCrawlFacade : BaseCrawlFacade<ReplyPost, ReplyResponse, Reply, ReplyCrawler>
    {
        private readonly TbmDbContext.New _dbContextFactory;
        private readonly InsertAllPostContentsIntoSonicWorker.SonicPusher _pusher;
        private readonly Tid _tid;

        public delegate ReplyCrawlFacade New(Fid fid, Tid tid);

        public ReplyCrawlFacade(ILogger<ReplyCrawlFacade> logger,
            TbmDbContext.New parentDbContextFactory, TbmDbContext.New dbContextFactory,
            ReplyCrawler.New crawler, ReplyParser parser, ReplySaver.New saver, UserParserAndSaver users,
            InsertAllPostContentsIntoSonicWorker.SonicPusher pusher,
            ClientRequesterTcs requesterTcs, IIndex<string, CrawlerLocks> locks, Fid fid, Tid tid
        ) : base(logger, parentDbContextFactory, crawler(fid, tid), parser, saver.Invoke, users, requesterTcs, (locks["reply"], new (fid, tid)), fid)
        {
            _dbContextFactory = dbContextFactory;
            _pusher = pusher;
            _tid = tid;
        }

        protected override void PostParseHook(ReplyResponse response, CrawlRequestFlag flag)
        {
            var data = response.Data;
            if (data.Page.CurrentPage == 1)
            { // update parent thread of reply with new title that extracted from the first floor reply in first page
                var db = _dbContextFactory(Fid);
                using var transaction = db.Database.BeginTransaction(IsolationLevel.ReadCommitted);
                var parentThreadTitle = (from t in db.Threads where t.Tid == _tid select t.Title).FirstOrDefault();
                if (parentThreadTitle == "")
                { // thread title will be empty string as a fallback when the thread author haven't write title for this thread
                    var newTitle = data.PostList.FirstOrDefault(r => r.Floor == 1)?.Title;
                    if (newTitle != null)
                    {
                        db.Attach(new ThreadPost {Tid = _tid, Title = newTitle}).Property(t => t.Title).IsModified = true;
                        if (db.SaveChanges() != 1) // do not touch UpdateAt field for the accuracy of time field in thread revisions
                            throw new DbUpdateException($"Parent thread title \"{newTitle}\" completion for tid {_tid} has failed.");
                        transaction.Commit();
                    }
                }
            }

            var users = data.UserList;
            if (!users.Any()) return;
            Users.ParseUsers(users);

            ParsedPosts.Values.IntersectBy(data.PostList.Select(r => r.Pid), r => r.Pid).ForEach(r => // only mutate posts which occurs in current response
            { // fill the values for some field of reply from user list which is out of post list
                var author = users.First(u => u.Uid == r.AuthorUid);
                r.AuthorManagerType = author.BawuType.NullIfWhiteSpace(); // will be null if he's not a moderator
                r.AuthorExpGrade = (ushort)author.LevelId; // will be null when author is a historical anonymous user
                r.Tid = _tid;
            });
        }

        protected override void PostCommitSaveHook(SaverChangeSet<ReplyPost> savedPosts) =>
            savedPosts.NewlyAdded.ForEach(r => _pusher.PushPost(Fid, "replies", r.Pid, r.Content));
    }
}
