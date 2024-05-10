using LinqToDB.DataProvider.PostgreSQL;

namespace tbm.Crawler.Tieba.Crawl.Saver;

public class AuthorRevisionSaver(PostType triggeredByPostType)
{
    // locks only using fid and uid field values from AuthorRevision
    // this prevents inserting multiple entities with similar time and other fields with the same values
    private static readonly HashSet<(Fid Fid, Uid Uid)> AuthorExpGradeLocks = [];
    private readonly List<(Fid Fid, Uid Uid)> _savedRevisions = [];

    public delegate AuthorRevisionSaver New(PostType triggeredByPostType);

    public Action SaveAuthorExpGradeRevisions<TPostWithAuthorExpGrade>
        (CrawlerDbContext db, IReadOnlyCollection<TPostWithAuthorExpGrade> posts)
        where TPostWithAuthorExpGrade : PostWithAuthorExpGrade
    {
        SaveAuthorRevisions(db, posts, AuthorExpGradeLocks,
            db.AuthorExpGradeRevisions,
            p => p.AuthorExpGrade,
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
        return () => ReleaseAllLocks(AuthorExpGradeLocks);
    }

    private void SaveAuthorRevisions<TPost, TRevision, TValue>(
        CrawlerDbContext db,
        IReadOnlyCollection<TPost> posts,
        HashSet<(Fid Fid, Uid Uid)> locks,
        IQueryable<TRevision> dbSet,
        Func<TPost, TValue?> postAuthorFieldValueSelector,
        Func<TValue?, TValue?, bool> isValueChangedPredicate,
        Expression<Func<TRevision, LatestAuthorRevisionProjection<TValue>>> latestRevisionProjectionFactory,
        Func<(Uid Uid, TValue? Value, Time DiscoveredAt), TRevision> revisionFactory)
        where TPost : BasePost
        where TRevision : AuthorRevision
    {
        Helper.GetNowTimestamp(out var now);
        var existingRevisionOfExistingUsers = dbSet.AsNoTracking()
            .Where(e => e.Fid == db.Fid
                        && posts.Select(p => p.AuthorUid).Distinct().Contains(e.Uid))
            .Select(latestRevisionProjectionFactory)
            .AsCte() // https://stackoverflow.com/questions/49854322/usage-of-for-update-in-window-function-postgres#comment86726589_49854322
            .Where(e => e.Rank == 1)
            .AsPostgreSQL().ForNoKeyUpdateHint()
            .ToLinqToDB().AsEnumerable()
            .Join(posts, e => e.Uid, p => p.AuthorUid, (e, p) =>
            (
                e.Uid,
                Existing: (e.DiscoveredAt, e.Value),
                NewInPost: (DiscoveredAt: now, Value: postAuthorFieldValueSelector(p))
            )).ToList();
        var newRevisionOfNewUsers = posts
            .ExceptBy(existingRevisionOfExistingUsers.Select(t => t.Uid), p => p.AuthorUid)
            .Select(p => (Uid: p.AuthorUid, Value: postAuthorFieldValueSelector(p), DiscoveredAt: now));
        var newRevisionOfExistingUsers = existingRevisionOfExistingUsers

            // filter out revisions with the same DiscoveredAt to prevent duplicate keys
            // when some fields get updated more than one time in a second
            .Where(t => t.Existing.DiscoveredAt != t.NewInPost.DiscoveredAt
                        && isValueChangedPredicate(t.Existing.Value, t.NewInPost.Value))
            .Select(t => (t.Uid, t.NewInPost.Value, t.NewInPost.DiscoveredAt));
        lock (locks)
        {
            var newRevisionsExceptLocked = newRevisionOfNewUsers
                .Concat(newRevisionOfExistingUsers)
                .Select(revisionFactory)
                .ExceptBy(locks, rev => (rev.Fid, rev.Uid))
                .ToList();
            if (newRevisionsExceptLocked.Count == 0) return;

            _savedRevisions.AddRange(newRevisionsExceptLocked.Select(rev => (rev.Fid, rev.Uid)));
            locks.UnionWith(_savedRevisions);
            db.Set<TRevision>().AddRange(newRevisionsExceptLocked);
        }
    }

    private void ReleaseAllLocks(HashSet<(Fid Fid, Uid Uid)> locks)
    {
        lock (locks) locks.ExceptWith(_savedRevisions);
    }

    private sealed class LatestAuthorRevisionProjection<TValue>
    {
        public Time DiscoveredAt { get; init; }
        public long Uid { get; init; }
        public TValue? Value { get; init; }
        public long Rank { get; init; }
    }
}
