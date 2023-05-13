using System.IO.Hashing;

namespace tbm.Crawler.Tieba.Crawl.Saver;

public class ReplySaver : BaseSaver<ReplyPost, BaseReplyRevision>
{
    public override FieldChangeIgnoranceCallbacks TiebaUserFieldChangeIgnorance { get; } = new(
        Update: (_, propName, oldValue, newValue) => propName switch
        { // FansNickname in reply response will always be null
            nameof(TiebaUser.FansNickname) when oldValue is not null && newValue is null => true,
            _ => false
        },
        Revision: (_, propName, oldValue, newValue) => propName switch
        { // user icon will be null after UserParserAndSaver.ResetUsersIcon() get invoked
            nameof(TiebaUser.Icon) when oldValue is null && newValue is not null => true,
            _ => false
        });

    protected override ushort GetRevisionNullFieldBitMask(string fieldName) => fieldName switch
    {
        nameof(ReplyPost.IsFold)        => 1 << 2,
        nameof(ReplyPost.DisagreeCount) => 1 << 4,
        nameof(ReplyPost.Geolocation)   => 1 << 5,
        _ => 0
    };

    protected override Dictionary<Type, Action<CrawlerDbContext, IEnumerable<BaseReplyRevision>>>
        RevisionUpsertPayloadKeyBySplitEntity { get; } = new()
    {
        {
            typeof(ReplyRevision.SplitFloor), (db, revisions) =>
                db.Set<ReplyRevision.SplitFloor>()
                    .UpsertRange(revisions.OfType<ReplyRevision.SplitFloor>()).NoUpdate().Run()
        },
        {
            typeof(ReplyRevision.SplitSubReplyCount), (db, revisions) =>
                db.Set<ReplyRevision.SplitSubReplyCount>()
                    .UpsertRange(revisions.OfType<ReplyRevision.SplitSubReplyCount>()).NoUpdate().Run()
        },
        {
            typeof(ReplyRevision.SplitAgreeCount), (db, revisions) =>
                db.Set<ReplyRevision.SplitAgreeCount>()
                    .UpsertRange(revisions.OfType<ReplyRevision.SplitAgreeCount>()).NoUpdate().Run()
        }
    };

    private record UniqueSignature(uint Id, byte[] XxHash3)
    {
        public virtual bool Equals(UniqueSignature? other) =>
            ReferenceEquals(this, other) || (other != null && Id == other.Id && XxHash3.SequenceEqual(other.XxHash3));
        // https://stackoverflow.com/questions/7244699/gethashcode-on-byte-array/72925335#72925335
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Id);
            hash.AddBytes(XxHash3);
            return hash.ToHashCode();
        }
    }
    private static readonly HashSet<UniqueSignature> SignatureLocks = new();
    private readonly List<UniqueSignature> _savedSignatures = new();

    public delegate ReplySaver New(ConcurrentDictionary<PostId, ReplyPost> posts);

    public ReplySaver(ILogger<ReplySaver> logger,
        ConcurrentDictionary<PostId, ReplyPost> posts,
        AuthorRevisionSaver.New authorRevisionSaverFactory
    ) : base(logger, posts, authorRevisionSaverFactory, "reply") { }

    public override SaverChangeSet<ReplyPost> SavePosts(CrawlerDbContext db)
    {
        var changeSet = SavePosts(db, r => r.Pid,
            r => new ReplyRevision {TakenAt = r.UpdatedAt ?? r.CreatedAt, Pid = r.Pid},
            PredicateBuilder.New<ReplyPost>(r => Posts.Keys.Contains(r.Pid)));

        db.ReplyContents.AddRange(changeSet.NewlyAdded
            .Select(r => new ReplyContent {Pid = r.Pid, Content = r.Content}));
        SaveReplyContentImages(db, changeSet.NewlyAdded);
        PostSaveEvent += AuthorRevisionSaver.SaveAuthorExpGradeRevisions(db, changeSet.AllAfter).Invoke;
        PostSaveEvent += SaveReplySignatures(db, changeSet.AllAfter).Invoke;

        return changeSet;
    }

    private Action SaveReplySignatures(CrawlerDbContext db, IEnumerable<ReplyPost> replies)
    {
        Helper.GetNowTimestamp(out var now);
        var signatures = replies
            .Where(r => r.SignatureId != null && r.Signature != null)
            .DistinctBy(r => r.SignatureId)
            .Select(r => new ReplySignature
            {
                UserId = r.AuthorUid,
                SignatureId = (uint)r.SignatureId!,
                SignatureXxHash3 = XxHash3.Hash(r.Signature!),
                Signature = r.Signature!,
                FirstSeenAt = now,
                LastSeenAt = now
            }).ToList();
        if (!signatures.Any()) return () => { };

        var uniqueSignatures = signatures
            .Select(s => new UniqueSignature(s.SignatureId, s.SignatureXxHash3)).ToList();
        var existingSignatures = (
            from s in db.ReplySignatures.AsTracking().TagWith("ForUpdate")
            where uniqueSignatures.Select(us => us.Id).Contains(s.SignatureId)
                  && uniqueSignatures.Select(us => us.XxHash3).Contains(s.SignatureXxHash3)
            select s
        ).ToList();
        (from existing in existingSignatures
                join newInReply in signatures on existing.SignatureId equals newInReply.SignatureId
                select (existing, newInReply))
            .ForEach(t => t.existing.LastSeenAt = t.newInReply.LastSeenAt);

        lock (SignatureLocks)
        {
            var newSignaturesExceptLocked = signatures
                .ExceptBy(existingSignatures.Select(s => s.SignatureId), s => s.SignatureId)
                .ExceptBy(SignatureLocks, s => new(s.SignatureId, s.SignatureXxHash3))
                .ToList();
            if (!newSignaturesExceptLocked.Any()) return () => { };

            _savedSignatures.AddRange(newSignaturesExceptLocked
                .Select(s => new UniqueSignature(s.SignatureId, s.SignatureXxHash3)));
            SignatureLocks.UnionWith(_savedSignatures);
            db.ReplySignatures.AddRange(newSignaturesExceptLocked);
        }
        return () =>
        {
            lock (SignatureLocks)
                if (_savedSignatures.Any()) SignatureLocks.ExceptWith(_savedSignatures);
        };
    }

    private static void SaveReplyContentImages(CrawlerDbContext db, IEnumerable<ReplyPost> replies)
    {
        var pidAndImageList = (
                from r in replies
                from c in r.OriginalContents
                where c.Type == 3
                // only save image filename without extension that extracted from url by ReplyParser.Convert()
                where ReplyParser.ValidateContentImageFilenameRegex.IsMatch(c.OriginSrc)
                select (r.Pid, Image: new TiebaImage
                {
                    UrlFilename = c.OriginSrc,
                    ByteSize = c.OriginSize
                }))
            .DistinctBy(t => (t.Pid, t.Image.UrlFilename))
            .ToList();
        if (!pidAndImageList.Any()) return;

        var imagesKeyByUrlFilename = pidAndImageList.Select(t => t.Image)
            .DistinctBy(image => image.UrlFilename).ToDictionary(image => image.UrlFilename);
        var existingImages = (from e in db.Images
            where imagesKeyByUrlFilename.Keys.Contains(e.UrlFilename)
            select e).TagWith("ForUpdate").ToDictionary(e => e.UrlFilename);
        (from existing in existingImages.Values
                where existing.ByteSize == 0 // randomly respond with 0
                join newInContent in imagesKeyByUrlFilename.Values on existing.UrlFilename equals newInContent.UrlFilename
                select (existing, newInContent))
            .ForEach(t => t.existing.ByteSize = t.newInContent.ByteSize);
        db.ReplyContentImages.AddRange(pidAndImageList.Select(t => new ReplyContentImage
        {
            Pid = t.Pid,
            // no need to manually invoke DbContent.AddRange(images) since EF Core will do these chore
            // https://stackoverflow.com/questions/5212751/how-can-i-retrieve-id-of-inserted-entity-using-entity-framework/41146434#41146434
            // reuse the same instance from imagesKeyByUrlFilename will prevent assigning multiple different instances with the same key
            // which will cause EF Core to insert identify entry more than one time leading to duplicated entry error
            // https://github.com/dotnet/efcore/issues/30236
            Image = existingImages.TryGetValue(t.Image.UrlFilename, out var e) ? e : imagesKeyByUrlFilename[t.Image.UrlFilename]
        }));
    }
}
