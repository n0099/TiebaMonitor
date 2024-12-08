namespace tbm.Crawler.Tieba.Crawl.Facade;

#pragma warning disable S3881 // "IDisposable" should be implemented correctly
public abstract class CrawlFacade<TPostEntity, TParsedPost, TResponse, TPostProtoBuf>(
#pragma warning restore S3881 // "IDisposable" should be implemented correctly
    BaseCrawler<TResponse, TPostProtoBuf> crawler,
    Fid fid,
    CrawlerLocks.LockId lockId,
    CrawlerLocks locks,
    IPostParser<TParsedPost, TPostProtoBuf> postParser,
    Func<ConcurrentDictionary<PostId, TParsedPost>, IPostSaver<TPostEntity, TParsedPost>> postSaverFactory,
    Func<ConcurrentDictionary<Uid, User.Parsed>, UserParser> userParserFactory,
    Func<ConcurrentDictionary<Uid, User.Parsed>, UserSaver> userSaverFactory)
    : ICrawlFacade<TPostEntity, TParsedPost>
    where TPostEntity : IPost
    where TParsedPost : TPostEntity, IPost.IParsed
    where TResponse : class, IMessage<TResponse>
    where TPostProtoBuf : class, IMessage<TPostProtoBuf>
{
    private readonly HashSet<Page> _lockingPages = [];
    private readonly ConcurrentDictionary<Uid, User.Parsed> _users = new();
    private ICrawlFacade<TPostEntity, TParsedPost>.ExceptionHandler _exceptionHandler = _ => { };

    // ReSharper disable UnusedAutoPropertyAccessor.Global
    public required ILogger<CrawlFacade<TPostEntity, TParsedPost, TResponse, TPostProtoBuf>>
        Logger { private get; init; }
    public required CrawlerDbContext.New DbContextFactory { private get; init; }
    public required ClientRequesterTcs RequesterTcs { private get; init; }

    // ReSharper restore UnusedAutoPropertyAccessor.Global
    protected Fid Fid { get; } = fid;
    protected ConcurrentDictionary<PostId, TParsedPost> Posts { get; } = new();

    [field: AllowNull, MaybeNull]
    protected UserParser UserParser => field ??= userParserFactory(_users);

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this); // https://github.com/dotnet/roslyn-analyzers/issues/4745
        locks.ReleaseRange(lockId, _lockingPages);
    }

    public SaverChangeSet<TPostEntity, TParsedPost>? SaveCrawled(CancellationToken stoppingToken = default)
    {
        var retryTimes = 0;
        while (true)
        {
            using var db = DbContextFactory(Fid); // dispose after each loop when retrying
            using var transaction = db.Database.BeginTransaction(IsolationLevel.ReadCommitted);

            var postSaver = postSaverFactory(Posts);
            var userSaver = userSaverFactory(_users);
            try
            {
                var savedPosts = Posts.IsEmpty ? null : postSaver.Save(db);
                userSaver.Save(db,
                    postSaver.CurrentPostType,
                    postSaver.UserFieldUpdateIgnorance,
                    postSaver.UserFieldRevisionIgnorance);

                OnBeforeCommitSave(db, userSaver);
                db.TimestampingEntities();
                _ = db.SaveChanges();
                transaction.Commit();

                if (savedPosts != null) OnPostCommitSave(savedPosts, stoppingToken);
                return savedPosts;
            }
            catch (DbUpdateConcurrencyException e)
            {
                db.LogDbUpdateConcurrencyException(e, ref retryTimes);
            }
            finally
            {
                postSaver.OnPostSave();
                userSaver.OnPostSave();
            }
        }
    }

    public async Task<ICrawlFacade<TPostEntity, TParsedPost>> CrawlPageRange(
        Page startPage,
        Page endPage = Page.MaxValue,
        CancellationToken stoppingToken = default)
    { // cancel when startPage is already locked
        if (_lockingPages.Count != 0) ThrowHelper.ThrowInvalidOperationException(
            "CrawlPageRange() can only be called once, a instance of CrawlFacade shouldn't be reuse for other crawls.");
        var acquiredLocks = locks.AcquireRange(lockId, [startPage]);
        if (acquiredLocks.Count == 0) Logger.LogInformation(
            "Cannot crawl any page within the range [{}-{}] for lock type {}, id {} since they've already been locked",
            startPage, endPage, locks.LockType, lockId);
        _lockingPages.UnionWith(acquiredLocks);

        var isStartPageCrawlFailed = await LogException(async () =>
        {
            var startPageResponse = await crawler.CrawlSinglePage(startPage, stoppingToken);
            startPageResponse.ForEach(ValidateThenParse);

            var maxPage = startPageResponse
                .Select(response => crawler.GetResponsePage(response.Result))
                .Max(page => (Page?)page?.TotalPage);
            endPage = Math.Min(endPage, maxPage ?? Page.MaxValue);
        }, startPage, previousFailureCount: 0, stoppingToken);

        // ReSharper disable once InvertIf
        if (!isStartPageCrawlFailed)
        {
            var pagesAfterStart = Enumerable.Range(
                (int)(startPage + 1),
                (int)(endPage - startPage)).ToList();
            if (pagesAfterStart.Count != 0)
                await CrawlPages(pagesAfterStart.ConvertAll(page => (Page)page), stoppingToken: stoppingToken);
        }
        return this;
    }

    public async Task<SaverChangeSet<TPostEntity, TParsedPost>?> RetryThenSave
        (IReadOnlyList<Page> pages, Func<Page, FailureCount> failureCountSelector, CancellationToken stoppingToken = default)
    {
        if (_lockingPages.Count != 0) ThrowHelper.ThrowInvalidOperationException(
            "RetryPages() can only be called once, a instance of CrawlFacade shouldn't be reuse for other crawls.");
        await CrawlPages(pages, failureCountSelector, stoppingToken);
        return SaveCrawled(stoppingToken);
    }

    public ICrawlFacade<TPostEntity, TParsedPost> AddExceptionHandler(
        ICrawlFacade<TPostEntity, TParsedPost>.ExceptionHandler handler)
    {
        _exceptionHandler += handler;
        return this;
    }

    protected virtual void ThrowIfEmptyUsersEmbedInPosts() { }
    protected virtual void OnPostParse(
        TResponse response,
        CrawlRequestFlag flag,
        IReadOnlyDictionary<PostId, TParsedPost> parsedPosts) { }
    protected virtual void OnBeforeCommitSave(CrawlerDbContext db, UserSaver userSaver) { }
    protected virtual void OnPostCommitSave(
        SaverChangeSet<TPostEntity, TParsedPost> savedPosts,
        CancellationToken stoppingToken = default) { }

    private void ValidateThenParse(BaseCrawler<TResponse, TPostProtoBuf>.Response responseTuple)
    {
        var (response, flag) = responseTuple;
        var postsInResponse = crawler.GetValidPosts(response, flag);
        postParser.Parse(flag, postsInResponse, out var parsedPostsInResponse, out var postsEmbeddedUsers);
        parsedPostsInResponse.ForEach(pair => Posts[pair.Key] = pair.Value);
        if (flag == CrawlRequestFlag.None)
        {
            if (postsEmbeddedUsers.Count == 0 && postsInResponse.Count != 0) ThrowIfEmptyUsersEmbedInPosts();
            if (postsEmbeddedUsers.Count != 0) UserParser.Parse(postsEmbeddedUsers);
        }
        OnPostParse(response, flag, parsedPostsInResponse);
    }

    private async Task CrawlPages(
        IReadOnlyList<Page> pages,
        Func<Page, FailureCount>? previousFailureCountSelector = null,
        CancellationToken stoppingToken = default)
    {
        var acquiredLocks = locks.AcquireRange(lockId, pages);
        if (acquiredLocks.Count == 0)
        {
            var pagesText = Enumerable
                .Range((int)pages[0], (int)pages[^1])
                .Select(page => (Page)page)
                .SequenceEqual(pages)
                ? $"within the range [{pages[0]}-{pages[^1]}]"
                : SharedHelper.UnescapedJsonSerialize(pages);
            Logger.LogInformation("Cannot crawl any page within {} for lock type {}, id {} since they've already been locked",
                pagesText, locks.LockType, lockId);
        }
        _lockingPages.UnionWith(acquiredLocks);

        _ = await Task.WhenAll(acquiredLocks.Shuffle()
            .Select(page => LogException(
                async () => (await crawler.CrawlSinglePage(page, stoppingToken)).ForEach(ValidateThenParse),
                page, previousFailureCountSelector?.Invoke(page) ?? 0, stoppingToken)));
    }

    private async Task<bool> LogException(
        Func<Task> payload,
        Page page,
        FailureCount previousFailureCount,
        CancellationToken stoppingToken = default)
    {
        try
        {
            await payload();
            return false;
        }
        catch (OperationCanceledException e) when (e.CancellationToken == stoppingToken)
        {
            throw;
        }
        catch (Exception e)
        {
            e.Data["page"] = page;
            e.Data["fid"] = Fid;
            e = crawler.FillExceptionData(e).ExtractInnerExceptionsData();

            if (e is TiebaException te)
            {
                if (!te.ShouldSilent) Logger.LogWarning("TiebaException: {} {}",
                    string.Join(' ', e.GetInnerExceptions().Select(ex => ex.Message)),
                    SharedHelper.UnescapedJsonSerialize(e.Data));
            }
            else
            {
                Logger.LogError(e, "Exception");
            }

            if (e is not TiebaException {ShouldRetry: false})
            {
                locks.AcquireFailed(lockId, page, (FailureCount)(previousFailureCount + 1));
                RequesterTcs.Decrease();
            }

            try
            {
                _exceptionHandler(e);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception");
                return true;
            }
            return true;
        }
    }
}
