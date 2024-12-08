namespace tbm.Crawler.Tieba.Crawl.Saver;

public partial class UserSaver(
    ILogger<UserSaver> logger,
    SaverLocks<Uid>.New saverLocksFactory,
    IDictionary<Uid, User.Parsed> users,
    ThreadLatestReplierSaver threadLatestReplierSaver,
    AuthorRevisionSaver.New authorRevisionSaverFactory)
    : SaverWithRevision<BaseUserRevision, Uid>(logger)
{
    private static readonly HashSet<Uid> GlobalLockedUid = [];
    private readonly Lazy<SaverLocks<Uid>> _saverLocks = new(() => saverLocksFactory(GlobalLockedUid));

    public delegate UserSaver New(IDictionary<Uid, User.Parsed> users);
    public delegate bool FieldChangeIgnorance(string propName, object? oldValue, object? newValue);

    private Action PostSaveHandlers { get; set; } = () => { };
    public void OnPostSave() => PostSaveHandlers();

    public void Save(
        CrawlerDbContext db,
        PostType postType,
        FieldChangeIgnorance userFieldUpdateIgnorance,
        FieldChangeIgnorance userFieldRevisionIgnorance)
    {
        if (users.Count == 0) return;
        var newlyLocked = _saverLocks.Value.Acquire(users.Keys());
        if (newlyLocked.Count == 0) return;
        PostSaveHandlers += _saverLocks.Value.Dispose;

        // existingUsers may have new revisions to insert so excluding already locked users
        // to prevent inserting duplicate revision
        var existingUsers = (from user in db.Users.AsTracking()
            where newlyLocked.Contains(user.Uid)
            select user).ToList();
        var maybeExistingAndNewUsers = (
            from newUser in users.IntersectByKey(newlyLocked).Values()
            join existingUser in existingUsers
                on newUser.Uid equals existingUser.Uid into existingUsersWithSameId
            from existingUser in existingUsersWithSameId.DefaultIfEmpty()
            select new MaybeExistingAndNewEntity<User>(existingUser, newUser)).ToList();

        var existingAndNewUsers = SaveNewEntities(db, maybeExistingAndNewUsers).ToList();
        SaveExistingEntities(db, existingAndNewUsers);
        SaveExistingEntityRevisions(db,
            u => new UserRevision
            {
                TakenAt = u.UpdatedAt ?? u.CreatedAt,
                Uid = u.Uid,
                TriggeredBy = postType
            },
            existingAndNewUsers,
            userFieldUpdateIgnorance,
            userFieldRevisionIgnorance);

        PostSaveHandlers += authorRevisionSaverFactory(postType).SaveAuthorExpGrade(db, users.Values.ToList());
    }

    public void SaveParentThreadLatestReplierUid(CrawlerDbContext db, Tid tid) =>
        PostSaveHandlers += threadLatestReplierSaver.SaveFromUser(db, tid, users.Values);
}
public partial class UserSaver
{
    [field: AllowNull, MaybeNull]
    protected override Lazy<Dictionary<Type, AddSplitRevisionsDelegate>>
        AddSplitRevisionsDelegatesKeyByEntityType =>
        field ??= new(() => new()
        {
            {typeof(UserRevision.SplitDisplayName), AddRevisionsWithDuplicateIndex<UserRevision.SplitDisplayName>},
            {typeof(UserRevision.SplitPortraitUpdatedAt), AddRevisionsWithDuplicateIndex<UserRevision.SplitPortraitUpdatedAt>},
            {typeof(UserRevision.SplitIpGeolocation), AddRevisionsWithDuplicateIndex<UserRevision.SplitIpGeolocation>}
        });

    protected override Uid RevisionIdSelector(BaseUserRevision entity) => entity.Uid;
    protected override Expression<Func<BaseUserRevision, bool>>
        IsRevisionIdEqualsExpression(BaseUserRevision newRevision) =>
        existingRevision => existingRevision.Uid == newRevision.Uid;
    protected override Expression<Func<BaseUserRevision, RevisionIdWithDuplicateIndexProjection>>
        RevisionIdWithDuplicateIndexProjectionFactory() =>
        e => new() {RevisionId = e.Uid, DuplicateIndex = e.DuplicateIndex};
}
public partial class UserSaver
{
    protected override bool FieldUpdateIgnorance
        (string propName, object? oldValue, object? newValue) => propName switch
    { // possible randomly respond with null
        nameof(User.IpGeolocation) when newValue is null => true,

        // possible clock drift across multiple response from tieba api
        // they should sync their servers with NTP
        /* following sql can track these drift
        SELECT portraitUpdatedAtDiff, COUNT(*), MAX(uid), MIN(uid), MAX(portraitUpdatedAt), MIN(portraitUpdatedAt)
        FROM (
            SELECT uid, portraitUpdatedAt, CAST(portraitUpdatedAt AS SIGNED)
                    - LEAD(CAST(portraitUpdatedAt AS SIGNED)) OVER (PARTITION BY uid ORDER BY time DESC) AS portraitUpdatedAtDiff
                FROM tbmcr_user WHERE portraitUpdatedAt IS NOT NULL
        ) AS T
        WHERE portraitUpdatedAtDiff > -100 AND portraitUpdatedAtDiff < 100
        GROUP BY portraitUpdatedAtDiff ORDER BY portraitUpdatedAtDiff;
        */
        nameof(User.PortraitUpdatedAt)
            when Math.Abs((newValue as int? ?? 0) - (oldValue as int? ?? 0)) <= 10 =>
            true,
        _ => false
    };

    protected override bool FieldRevisionIgnorance
        (string propName, object? oldValue, object? newValue) => propName switch
    { // ignore revision that figures update existing old users that don't have ip geolocation
        nameof(User.IpGeolocation) when oldValue is null => true,
        _ => false
    };

    [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1025:Code should not contain multiple whitespace in a row")]
    protected override NullFieldsBitMask GetRevisionNullFieldBitMask(string fieldName) => fieldName switch
    {
        nameof(User.Name)   => 1,
        nameof(User.Gender) => 1 << 3,
        nameof(User.Icon)   => 1 << 5,
        _ => 0
    };
}
