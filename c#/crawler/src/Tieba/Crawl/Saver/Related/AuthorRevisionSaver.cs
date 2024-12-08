namespace tbm.Crawler.Tieba.Crawl.Saver.Related;

public class AuthorRevisionSaver(
    ILogger<AuthorRevisionSaver> logger,
    SaverLocks<AuthorRevisionSaver.UniqueAuthorRevision>.New saverLocksFactory,
    PostType triggeredByPostType)
{
    private static readonly HashSet<UniqueAuthorRevision> GlobalLockedAuthorExpGrades = [];
    private readonly Lazy<SaverLocks<UniqueAuthorRevision>> _authorExpGradeSaverLocks =
        new(() => saverLocksFactory(GlobalLockedAuthorExpGrades));

    public delegate AuthorRevisionSaver New(PostType triggeredByPostType);

    public Action SaveAuthorExpGrade(CrawlerDbContext db, IReadOnlyCollection<User.Parsed> users) =>
        Save(db, users,
            _authorExpGradeSaverLocks.Value,
            db.AuthorExpGradeRevisions,
            e => e.ExpGrade,
            e => e.Uid,
            (a, b) => a != b,
            rev => new()
            {
                DiscoveredAt = rev.DiscoveredAt,
                Uid = rev.Uid,
                Value = rev.AuthorExpGrade,
                Rank = Sql.Ext.Rank().Over().PartitionBy(rev.Uid).OrderByDesc(rev.DiscoveredAt).ToValue()
            },
            t => new()
            {
                DiscoveredAt = t.DiscoveredAt,
                Fid = db.Fid,
                Uid = t.Uid,
                TriggeredBy = triggeredByPostType,
                AuthorExpGrade = t.Value
            });

    private Action Save<TEntity, TRevision, TValue>(
        CrawlerDbContext db,
        IReadOnlyCollection<TEntity> entities,
        SaverLocks<UniqueAuthorRevision> locks,
        IQueryable<TRevision> dbSet,
        Func<TEntity, TValue?> revisioningFieldSelector,
        Func<TEntity, long> uidSelector,
        Func<TValue?, TValue?, bool> isValueChangedPredicate,
        Expression<Func<TRevision, LatestAuthorRevisionProjection<TValue>>> latestRevisionProjectionFactory,
        Func<(Uid Uid, TValue? Value, Time DiscoveredAt), TRevision> revisionFactory)
        where TEntity : RowVersionedEntity
        where TRevision : AuthorRevision
    { // only takes the first of multiple entity from the same author
        var uniqueEntities = entities.DistinctBy(uidSelector).ToList();
        if (uniqueEntities.Count != entities.Count)
            Helper.LogDifferentValuesSharingTheSameKeyInEntities(logger, entities,
            $"{nameof(TEntity)}.{nameof(IPost.AuthorUid)}",
            uidSelector,
            revisioningFieldSelector);

        SharedHelper.GetNowTimestamp(out var now);
        var usersId = uniqueEntities.Select(uidSelector);
        var existingRevisionOfExistingUsers = dbSet.AsNoTracking()
            .Where(e => e.Fid == db.Fid && usersId.Contains(e.Uid))
            .Select(latestRevisionProjectionFactory)
            .AsCte() // https://stackoverflow.com/questions/49854322/usage-of-for-update-in-window-function-postgres#comment86726589_49854322
            .Where(e => e.Rank == 1)
            .ToLinqToDB().AsEnumerable()
            .Join(uniqueEntities, e => e.Uid, uidSelector, (e, p) =>
            (
                e.Uid,
                Existing: (e.DiscoveredAt, e.Value),
                NewInEntity: (DiscoveredAt: now, Value: revisioningFieldSelector(p))
            )).ToList();
        var newRevisionOfExistingUsers = existingRevisionOfExistingUsers

            // filter out revisions with the same DiscoveredAt to prevent duplicate keys
            // when some fields get updated more than once in a second
            .Where(t => t.Existing.DiscoveredAt != t.NewInEntity.DiscoveredAt
                        && isValueChangedPredicate(t.Existing.Value, t.NewInEntity.Value))
            .Select(t => (t.Uid, t.NewInEntity.Value, t.NewInEntity.DiscoveredAt));
        var newRevisionOfNewUsers = uniqueEntities
            .ExceptBy(existingRevisionOfExistingUsers.Select(t => t.Uid), uidSelector)
            .Select(e => (Uid: uidSelector(e), Value: revisioningFieldSelector(e), DiscoveredAt: now));
        var newRevisions = newRevisionOfNewUsers
            .Concat(newRevisionOfExistingUsers)
            .Select(revisionFactory)
            .ToDictionary(revision => new UniqueAuthorRevision(revision.Fid, revision.Uid), i => i);
        db.Set<TRevision>().AddRange(newRevisions
            .IntersectByKey(locks.Acquire(newRevisions.Keys))
            .Values());
        return locks.Dispose;
    }

    // locking key only using AuthorRevision.Fid and Uid, ignoring TriggeredBy
    // this prevents inserting multiple entities with similar time and other fields with the same values
    // ReSharper disable once MemberCanBePrivate.Global
    public record UniqueAuthorRevision(Fid Fid, Uid Uid);

    private sealed class LatestAuthorRevisionProjection<TValue>
    {
        public Time DiscoveredAt { get; init; }
        public long Uid { get; init; }
        public TValue? Value { get; init; }
        public long Rank { get; init; }
    }
}
