namespace tbm.Crawler.Tieba.Crawl.Saver
{
    public class ReplySaver : BaseSaver<ReplyPost>
    {
        public override FieldChangeIgnoranceCallbackRecord TiebaUserFieldChangeIgnorance { get; } = new(
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

        protected override Dictionary<string, ushort> RevisionNullFieldsBitMasks { get; } = new()
        {
            {nameof(ReplyPost.AuthorManagerType), 1},
            {nameof(ReplyPost.IsFold),            1 << 2},
            {nameof(ReplyPost.DisagreeCount),     1 << 4},
            {nameof(ReplyPost.Geolocation),       1 << 5}
        };

        private record UniqueSignature(uint Id, byte[] Md5)
        {
            public virtual bool Equals(UniqueSignature? other) =>
                ReferenceEquals(this, other) || (other != null && Id == other.Id && Md5.SequenceEqual(other.Md5));
            // https://stackoverflow.com/questions/7244699/gethashcode-on-byte-array/72925335#72925335
            public override int GetHashCode()
            {
                var hash = new HashCode();
                hash.Add(Id);
                hash.AddBytes(Md5);
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

        public override SaverChangeSet<ReplyPost> SavePosts(TbmDbContext db)
        {
            var changeSet = SavePosts(db, r => r.Pid, r => (long)r.Pid,
                r => new ReplyRevision {TakenAt = r.UpdatedAt ?? r.CreatedAt, Pid = r.Pid},
                PredicateBuilder.New<ReplyPost>(r => Posts.Keys.Contains(r.Pid)),
                newRevisions => existing => newRevisions.Select(r => r.Pid).Contains(existing.Pid),
                r => new() {TakenAt = r.TakenAt, Pid = r.Pid});

            db.ReplyContents.AddRange(changeSet.NewlyAdded.Select(r => new ReplyContent {Pid = r.Pid, Content = r.Content}));
            PostSaveEvent += AuthorRevisionSaver.SaveAuthorExpGradeRevisions(db, changeSet.AllAfter).Invoke;
            PostSaveEvent += SaveReplySignatures(db, changeSet.AllAfter).Invoke;

            return changeSet;
        }

        private Action SaveReplySignatures(TbmDbContext db, IEnumerable<ReplyPost> replies)
        {
            var now = (Time)DateTimeOffset.Now.ToUnixTimeSeconds();
            var signatures = replies
                .Where(r => r.SignatureId != null && r.Signature != null)
                .DistinctBy(r => r.SignatureId)
                .Select(r => new ReplySignature
                {
                    UserId = r.AuthorUid,
                    SignatureId = (uint)r.SignatureId!,
                    SignatureMd5 = MD5.HashData(r.Signature!),
                    Signature = r.Signature!,
                    FirstSeenAt = now,
                    LastSeenAt = now
                }).ToList();
            if (!signatures.Any()) return () => { };

            var uniqueSignatures = signatures
                .Select(s => new UniqueSignature(s.SignatureId, s.SignatureMd5)).ToList();
            var existingSignatures = (from s in db.ReplySignatures.AsTracking().TagWith("ForUpdate")
                where uniqueSignatures.Select(us => us.Id).Contains(s.SignatureId)
                      && uniqueSignatures.Select(us => us.Md5).Contains(s.SignatureMd5)
                select s).ToList();
            existingSignatures.Join(signatures, s => s.SignatureId, s => s.SignatureId,
                    (existing, newInReply) => (existing, newInReply))
                .ForEach(tuple => tuple.existing.LastSeenAt = tuple.newInReply.LastSeenAt);

            lock (SignatureLocks)
            {
                var newSignaturesExceptLocked = signatures
                    .ExceptBy(existingSignatures.Select(s => s.SignatureId), s => s.SignatureId)
                    .ExceptBy(SignatureLocks, s => new(s.SignatureId, s.SignatureMd5)).ToList();
                if (!newSignaturesExceptLocked.Any()) return () => { };

                _savedSignatures.AddRange(newSignaturesExceptLocked
                    .Select(s => new UniqueSignature(s.SignatureId, s.SignatureMd5)));
                SignatureLocks.UnionWith(_savedSignatures);
                db.ReplySignatures.AddRange(newSignaturesExceptLocked);
            }
            return () =>
            {
                lock (SignatureLocks)
                    if (_savedSignatures.Any()) SignatureLocks.ExceptWith(_savedSignatures);
            };
        }
    }
}
