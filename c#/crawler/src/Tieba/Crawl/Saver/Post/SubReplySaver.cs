namespace tbm.Crawler.Tieba.Crawl.Saver.Post;

public partial class SubReplySaver(
    ILogger<SubReplySaver> logger,
    ConcurrentDictionary<PostId, SubReplyPost.Parsed> posts)
    : PostSaver<SubReplyPost, SubReplyPost.Parsed, BaseSubReplyRevision, Spid>(
        logger, posts, PostType.SubReply)
{
    public delegate SubReplySaver New(ConcurrentDictionary<PostId, SubReplyPost.Parsed> posts);

    public override SaverChangeSet<SubReplyPost, SubReplyPost.Parsed> Save(CrawlerDbContext db)
    {
        var changeSet = Save(db, sr => sr.Spid,
            sr => new SubReplyRevision {TakenAt = sr.UpdatedAt ?? sr.CreatedAt, Spid = sr.Spid},
            posts => posts.Where(sr => Posts.Keys.Contains(sr.Spid)));

        db.SubReplyContents.AddRange(changeSet.NewlyAdded.Select(sr => // https://github.com/dotnet/efcore/issues/33945
            new SubReplyContent {Spid = sr.Spid, ProtoBufBytes = sr.Content}));

        return changeSet;
    }
}
public partial class SubReplySaver
{
    [field: AllowNull, MaybeNull]
    protected override Lazy<Dictionary<Type, AddSplitRevisionsDelegate>>
        AddSplitRevisionsDelegatesKeyByEntityType =>
        field ??= new(() => new()
        {
            {typeof(SubReplyRevision.SplitAgreeCount), AddRevisionsWithDuplicateIndex<SubReplyRevision.SplitAgreeCount>},
            {typeof(SubReplyRevision.SplitDisagreeCount), AddRevisionsWithDuplicateIndex<SubReplyRevision.SplitDisagreeCount>}
        });

    protected override Spid RevisionIdSelector(BaseSubReplyRevision entity) => entity.Spid;
    protected override Expression<Func<BaseSubReplyRevision, bool>>
        IsRevisionIdEqualsExpression(BaseSubReplyRevision newRevision) =>
        existingRevision => existingRevision.Spid == newRevision.Spid;
    protected override Expression<Func<BaseSubReplyRevision, RevisionIdWithDuplicateIndexProjection>>
        RevisionIdWithDuplicateIndexProjectionFactory() =>
        e => new() {RevisionId = e.Spid, DuplicateIndex = e.DuplicateIndex};
}
public partial class SubReplySaver
{
    public override bool UserFieldUpdateIgnorance
        (string propName, object? oldValue, object? newValue) => propName switch
    { // always ignore updates on iconinfo due to some rare user will show some extra icons
        // compare to reply response in the response of sub reply
        nameof(User.Icon) => true,

        // FansNickname in sub reply response will always be null
        nameof(User.FansNickname) when newValue is null && oldValue is not null => true,

        // DisplayName in users embedded in sub replies from response will be the legacy nickname
        nameof(User.DisplayName) => true,
        _ => false
    };

    protected override NullFieldsBitMask GetRevisionNullFieldBitMask(string fieldName) => 0;
}
