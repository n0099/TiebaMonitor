using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using static tbm.Crawler.Db.Revision.Splitting.ReplyRevision;
using static tbm.Crawler.Db.Revision.Splitting.SubReplyRevision;
using static tbm.Crawler.Db.Revision.Splitting.ThreadRevision;
using static tbm.Crawler.Db.Revision.Splitting.UserRevision;

namespace tbm.Crawler.Db;

public class CrawlerDbContext(ILogger<CrawlerDbContext> logger, Fid fid = 0)
    : TbmDbContext<CrawlerDbContext.ModelCacheKeyFactory>(logger)
{
    public delegate CrawlerDbContext NewDefault();
    public delegate CrawlerDbContext New(Fid fid);

    public Fid Fid { get; } = fid;
    public DbSet<User> Users => Set<User>();
    public DbSet<LatestReplier> LatestRepliers => Set<LatestReplier>();
    public DbSet<LatestReplierRevision> LatestReplierRevisions => Set<LatestReplierRevision>();
    public DbSet<AuthorExpGradeRevision> AuthorExpGradeRevisions => Set<AuthorExpGradeRevision>();
    public DbSet<ForumModeratorRevision> ForumModeratorRevisions => Set<ForumModeratorRevision>();
    public DbSet<ThreadPost> Threads => Set<ThreadPost>();
    public DbSet<ThreadMissingFirstReply> ThreadMissingFirstReplies => Set<ThreadMissingFirstReply>();
    public DbSet<ReplyPost> Replies => Set<ReplyPost>();
    public DbSet<ReplySignature> ReplySignatures => Set<ReplySignature>();
    public DbSet<ReplyContent> ReplyContents => Set<ReplyContent>();
    public DbSet<SubReplyContent> SubReplyContents => Set<SubReplyContent>();
    public DbSet<Forum> Forums => Set<Forum>();

    public void TimestampingEntities() =>

        // https://www.entityframeworktutorial.net/faq/set-created-and-modified-date-in-efcore.aspx
        ChangeTracker.Entries<TimestampedEntity>().ForEach(e =>
        {
            SharedHelper.GetNowTimestamp(out var now);
            var originalEntityState = e.State; // copy e.State since it might change after any prop value updated
            var createdAtProp = e.Property(ie => ie.CreatedAt);
            var updatedAtProp = e.Property(ie => ie.UpdatedAt);
            var lastSeenAtProp = e.Entity is IPost ? e.Property(ie => ((IPost)ie).LastSeenAt) : null;

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (originalEntityState)
            { // mutates Entry.CurrentValue will always update Entry.IsModified
                // and the value of corresponding field in entity class instance
                // that ChangeTracker references to, aka Entry.Entity
                // while mutating Entry.Entity.Field requires (im|ex)plicitly
                // invoking DetectChanges() to update Entry.CurrentValue and IsModified
                case EntityState.Added:
                    createdAtProp.CurrentValue = now;
                    break;
                case EntityState.Modified when createdAtProp.CurrentValue != now:
                    updatedAtProp.CurrentValue = now;
                    break;
            }
            if (lastSeenAtProp != null)
            {
                lastSeenAtProp.CurrentValue = originalEntityState switch
                {
                    EntityState.Unchanged => now, // updatedAt won't change when entity is unchanged
                    EntityState.Modified => null, // null means it's same with updatedAt
                    _ => lastSeenAtProp.CurrentValue
                };
            }
        });

    [SuppressMessage("Style", "IDE0058:Expression value is never used")]
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        OnModelCreatingWithFid(b, Fid);
        b.Entity<User>().ToTable("tbmc_user");
        b.Entity<LatestReplier>().ToTable("tbmc_latestReplier");
        b.Entity<LatestReplier>().Property(e => e.DisplayName).HasConversion<byte[]>();
        b.Entity<LatestReplierRevision>().ToTable("tbmcr_latestReplier").HasKey(e => new {e.TakenAt, e.Uid});
        b.Entity<LatestReplierRevision>().Property(e => e.DisplayName).HasConversion<byte[]>();
        b.Entity<ThreadPost>().ToTable($"tbmc_f{Fid}_thread")
            .HasOne<LatestReplier>(e => e.LatestReplier).WithMany().HasForeignKey(e => e.LatestReplierId);
        b.Entity<ThreadMissingFirstReply>().ToTable("tbmc_thread_missingFirstReply");
        b.Entity<ReplyPost>().ToTable($"tbmc_f{Fid}_reply");
        b.Entity<ReplyContent>().ToTable($"tbmc_f{Fid}_reply_content");
        b.Entity<ReplySignature>().ToTable("tbmc_reply_signature").HasKey(e => new {e.SignatureId, e.XxHash3});
        b.Entity<SubReplyPost>().ToTable($"tbmc_f{Fid}_subReply");
        b.Entity<SubReplyContent>().ToTable($"tbmc_f{Fid}_subReply_content");

        _ = new RevisionWithSplitting<BaseThreadRevision>
                .ModelBuilder(b, "tbmcr_thread", e => new {e.Tid, e.TakenAt, e.DuplicateIndex})
            .HasBaseTable<ThreadRevision>()
            .HasSplitTable<SplitViewCount>("viewCount");

        _ = new RevisionWithSplitting<BaseReplyRevision>
                .ModelBuilder(b, "tbmcr_reply", e => new {e.Pid, e.TakenAt, e.DuplicateIndex})
            .HasBaseTable<ReplyRevision>()
            .HasSplitTable<ReplyRevision.SplitAgreeCount>("agreeCount")
            .HasSplitTable<SplitSubReplyCount>("subReplyCount")
            .HasSplitTable<SplitFloor>("floor");

        _ = new RevisionWithSplitting<BaseSubReplyRevision>
                .ModelBuilder(b, "tbmcr_subReply", e => new {e.Spid, e.TakenAt, e.DuplicateIndex})
            .HasBaseTable<SubReplyRevision>()
            .HasSplitTable<SubReplyRevision.SplitAgreeCount>("agreeCount")
            .HasSplitTable<SplitDisagreeCount>("disagreeCount");

        _ = new RevisionWithSplitting<BaseUserRevision>
                .ModelBuilder(b, "tbmcr_user", e => new {e.Uid, e.TakenAt, e.DuplicateIndex})
            .HasBaseTable<UserRevision>()
            .HasSplitTable<SplitIpGeolocation>("ipGeolocation")
            .HasSplitTable<SplitPortraitUpdatedAt>("portraitUpdatedAt")
            .HasSplitTable<SplitDisplayName>("displayName");

        b.Entity<SplitDisplayName>().Property(e => e.DisplayName).HasConversion<byte[]>();
        b.Entity<User>().Property(e => e.DisplayName).HasConversion<byte[]>();

        b.Entity<AuthorExpGradeRevision>().ToTable("tbmcr_authorExpGrade")
            .HasKey(e => new {e.Fid, e.Uid, e.DiscoveredAt});
        b.Entity<ForumModeratorRevision>().ToTable("tbmcr_forumModerator")
            .HasKey(e => new {e.Fid, e.Portrait, e.DiscoveredAt, e.ModeratorTypes});
        b.Entity<Forum>().ToTable("tbm_forum");
    }

    protected override void OnConfiguringNpgsql(NpgsqlDbContextOptionsBuilder builder) =>
        builder.MapEnum<PostType>("tbmcr_triggeredBy", nameTranslator: NpgsqlCamelCaseNameTranslator.Instance);

    public class ModelCacheKeyFactory : IModelCacheKeyFactory
    { // https://stackoverflow.com/questions/51864015/entity-framework-map-model-class-to-table-at-run-time/51899590#51899590
        // https://docs.microsoft.com/en-us/ef/core/modeling/dynamic-model
        public object Create(DbContext context, bool designTime) =>
            context is CrawlerDbContext dbContext
                ? (context.GetType(), dbContext.Fid, designTime)
                : context.GetType();
    }
}
