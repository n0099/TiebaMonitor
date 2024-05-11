using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using static tbm.Crawler.Db.Revision.Splitting.ReplyRevision;
using static tbm.Crawler.Db.Revision.Splitting.SubReplyRevision;
using static tbm.Crawler.Db.Revision.Splitting.ThreadRevision;
using static tbm.Crawler.Db.Revision.Splitting.UserRevision;

namespace tbm.Crawler.Db;

public class CrawlerDbContext(ILogger<CrawlerDbContext> logger, Fid fid = 0)
    : TbmDbContext<CrawlerDbContext.ModelCacheKeyFactory>(logger)
{
    private static Lazy<NpgsqlDataSource>? _dataSourceSingleton;

    public delegate CrawlerDbContext NewDefault();
    public delegate CrawlerDbContext New(Fid fid);

    public Fid Fid { get; } = fid;
    public DbSet<User> Users => Set<User>();
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
            Helper.GetNowTimestamp(out var now);
            var originalEntityState = e.State; // copy e.State since it might change after any prop value updated
            var createdAtProp = e.Property(ie => ie.CreatedAt);
            var updatedAtProp = e.Property(ie => ie.UpdatedAt);
            var lastSeenAtProp = e.Entity is BasePost ? e.Property(ie => ((BasePost)ie).LastSeenAt) : null;

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
        b.Entity<ThreadPost>().ToTable($"tbmc_f{Fid}_thread");
        b.Entity<ThreadMissingFirstReply>().ToTable("tbmc_thread_missingFirstReply");
        b.Entity<ReplyPost>().ToTable($"tbmc_f{Fid}_reply");
        b.Entity<ReplySignature>().ToTable("tbmc_reply_signature").HasKey(e => new {e.SignatureId, e.XxHash3});
        b.Entity<ReplyContent>().ToTable($"tbmc_f{Fid}_reply_content");
        b.Entity<SubReplyPost>().ToTable($"tbmc_f{Fid}_subReply");
        b.Entity<SubReplyContent>().ToTable($"tbmc_f{Fid}_subReply_content");

        var thread = new RevisionWithSplitting<BaseThreadRevision>.ModelBuilderExtension(b, "tbmcr_thread");
        thread.HasKey<ThreadRevision>(e => new {e.Tid, e.TakenAt});
        thread.SplittingHasKey<SplitViewCount>("viewCount", e => new {e.Tid, e.TakenAt});

        var reply = new RevisionWithSplitting<BaseReplyRevision>.ModelBuilderExtension(b, "tbmcr_reply");
        reply.HasKey<ReplyRevision>(e => new {e.Pid, e.TakenAt});
        reply.SplittingHasKey<ReplyRevision.SplitAgreeCount>("agreeCount", e => new {e.Pid, e.TakenAt});
        reply.SplittingHasKey<SplitSubReplyCount>("subReplyCount", e => new {e.Pid, e.TakenAt});
        reply.SplittingHasKey<SplitFloor>("floor", e => new {e.Pid, e.TakenAt});

        var subReply = new RevisionWithSplitting<BaseSubReplyRevision>.ModelBuilderExtension(b, "tbmcr_subReply");
        subReply.HasKey<SubReplyRevision>(e => new {e.Spid, e.TakenAt});
        subReply.SplittingHasKey<SubReplyRevision.SplitAgreeCount>("agreeCount", e => new {e.Spid, e.TakenAt});
        subReply.SplittingHasKey<SplitDisagreeCount>("disagreeCount", e => new {e.Spid, e.TakenAt});

        var user = new RevisionWithSplitting<BaseUserRevision>.ModelBuilderExtension(b, "tbmcr_user");
        user.HasKey<UserRevision>(e => new {e.Uid, e.TakenAt});
        user.SplittingHasKey<SplitIpGeolocation>("ipGeolocation", e => new {e.Uid, e.TakenAt});
        user.SplittingHasKey<SplitPortraitUpdatedAt>("portraitUpdatedAt", e => new {e.Uid, e.TakenAt});
        user.SplittingHasKey<SplitDisplayName>("displayName", e => new {e.Uid, e.TakenAt});
        b.Entity<SplitDisplayName>().Property(e => e.DisplayName).HasConversion<byte[]>();
        b.Entity<User>().Property(e => e.DisplayName).HasConversion<byte[]>();

        b.Entity<AuthorExpGradeRevision>().ToTable("tbmcr_authorExpGrade")
            .HasKey(e => new {e.Fid, e.Uid, e.DiscoveredAt});
        b.Entity<ForumModeratorRevision>().ToTable("tbmcr_forumModerator")
            .HasKey(e => new {e.Fid, e.Portrait, e.DiscoveredAt, e.ModeratorTypes});
        b.Entity<Forum>().ToTable("tbm_forum");
    }

    protected override void OnBuildingNpgsqlDataSource(NpgsqlDataSourceBuilder builder) =>
        builder.MapEnum<PostType>("tbmcr_triggeredBy", new NpgsqlCamelCaseNameTranslator());

    [SuppressMessage("Critical Code Smell", "S2696:Instance members should not write to \"static\" fields")]
    protected override Lazy<NpgsqlDataSource> GetNpgsqlDataSource(string? connectionString) =>
        _dataSourceSingleton ??= GetNpgsqlDataSourceFactory(connectionString);

    public class ModelCacheKeyFactory : IModelCacheKeyFactory
    { // https://stackoverflow.com/questions/51864015/entity-framework-map-model-class-to-table-at-run-time/51899590#51899590
        // https://docs.microsoft.com/en-us/ef/core/modeling/dynamic-model
        public object Create(DbContext context, bool designTime) =>
            context is CrawlerDbContext dbContext
                ? (context.GetType(), dbContext.Fid, designTime)
                : context.GetType();
    }
}
