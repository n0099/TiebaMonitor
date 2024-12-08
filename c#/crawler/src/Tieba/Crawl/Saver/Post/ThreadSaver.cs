namespace tbm.Crawler.Tieba.Crawl.Saver.Post;

public partial class ThreadSaver(
    ILogger<ThreadSaver> logger,
    ConcurrentDictionary<Tid, ThreadPost.Parsed> posts,
    ThreadLatestReplierSaver threadLatestReplierSaver)
    : PostSaver<ThreadPost, ThreadPost.Parsed, BaseThreadRevision, Tid>(
        logger, posts, PostType.Thread)
{
    public delegate ThreadSaver New(ConcurrentDictionary<Tid, ThreadPost.Parsed> posts);

    public override SaverChangeSet<ThreadPost, ThreadPost.Parsed> Save(CrawlerDbContext db) => Save(db,
        th => th.Tid,
        th => new ThreadRevision {TakenAt = th.UpdatedAt ?? th.CreatedAt, Tid = th.Tid},
        posts => posts
            .Where(th => Posts.Keys.Contains(th.Tid))
            .Include(th => th.LatestReplier),
        maybeEntities =>
            PostSaveHandlers += threadLatestReplierSaver.SaveFromThread(db,
                maybeEntities.Select(entity => entity.Existing ?? entity.New).ToList()));
}
public partial class ThreadSaver
{
    [field: AllowNull, MaybeNull]
    protected override Lazy<Dictionary<Type, AddSplitRevisionsDelegate>>
        AddSplitRevisionsDelegatesKeyByEntityType =>
        field ??= new(() => new()
        {
            {typeof(ThreadRevision.SplitViewCount), AddRevisionsWithDuplicateIndex<ThreadRevision.SplitViewCount>}
        });

    protected override Tid RevisionIdSelector(BaseThreadRevision entity) => entity.Tid;
    protected override Expression<Func<BaseThreadRevision, bool>>
        IsRevisionIdEqualsExpression(BaseThreadRevision newRevision) =>
        existingRevision => existingRevision.Tid == newRevision.Tid;
    protected override Expression<Func<BaseThreadRevision, RevisionIdWithDuplicateIndexProjection>>
        RevisionIdWithDuplicateIndexProjectionFactory() =>
        e => new() {RevisionId = e.Tid, DuplicateIndex = e.DuplicateIndex};
}
public partial class ThreadSaver
{
    public override bool UserFieldUpdateIgnorance
        (string propName, object? oldValue, object? newValue) => propName switch
    { // Icon.SpriteInfo will be an empty array and the icon url is a smaller one
        // so we should mark it as null temporarily
        // note this will cause we can't record when did a user update its iconinfo to null
        // since these null values have been ignored in ReplySaver and SubReplySaver
        nameof(User.Icon) => true,
        _ => false
    };

    protected override bool FieldUpdateIgnorance
        (string propName, object? oldValue, object? newValue) => propName switch
    { // will be updated by ThreadLateCrawler and ThreadLateCrawlFacade
        nameof(ThreadPost.AuthorPhoneType) => true,

        // prevent overwrite existing value of field liker_id which is saved by legacy crawler
        // and Zan itself is deprecated by tieba, so it shouldn't get updated
        nameof(ThreadPost.Zan) => true,

        // possible randomly respond with null
        nameof(ThreadPost.Geolocation) when newValue is null => true,

        // empty string means the author had not written a title
        // its value generated from the first reply within response of reply crawler
        // will be later set by ReplyCrawlFacade.SaveParentThreadTitle()
        nameof(ThreadPost.Title)
            when newValue is ""

            // prevent update repeatedly with different title
            // due to the thread is a multi forum topic thread
            // thus its title can be varied within the forum and within the thread
            || (newValue is not "" && oldValue is not "") => true,

        // possible randomly respond with 0.NullIfZero()
        nameof(ThreadPost.DisagreeCount) when newValue is null && oldValue is not null => true,

        // when the latest reply post is deleted and there's no new reply after delete
        // this field but not LatestReplyPostedAt will be null
        nameof(ThreadPost.LatestReplierId) when newValue is null => true,
        _ => false
    };

    protected override bool FieldRevisionIgnorance
        (string propName, object? oldValue, object? newValue) => propName switch
    { // empty string from response has been updated by ReplyCrawlFacade.OnPostParse()
        nameof(ThreadPost.Title) when oldValue is "" => true,
        _ => false
    };

    [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1025:Code should not contain multiple whitespace in a row")]
    protected override NullFieldsBitMask GetRevisionNullFieldBitMask(string fieldName) => fieldName switch
    {
        nameof(ThreadPost.StickyType)      => 1,
        nameof(ThreadPost.TopicType)       => 1 << 1,
        nameof(ThreadPost.IsGood)          => 1 << 2,
        nameof(ThreadPost.LatestReplierId) => 1 << 4,
        nameof(ThreadPost.ReplyCount)      => 1 << 5,
        nameof(ThreadPost.ShareCount)      => 1 << 7,
        nameof(ThreadPost.AgreeCount)      => 1 << 8,
        nameof(ThreadPost.DisagreeCount)   => 1 << 9,
        nameof(ThreadPost.Geolocation)     => 1 << 10,
        _ => 0
    };
}
