using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace tbm.Shared.Db;

public abstract class TbmDbContext : DbContext
{
    public int SaveChangesForUpdate()
    {
        while (true)
        {
            try
            {
                return SaveChanges();
            }
            catch (DbUpdateConcurrencyException e)
            {
                foreach (var entry in e.Entries)
                {
                    var existing = entry.GetDatabaseValues();
                    if (existing == null) entry.State = EntityState.Added; // already deleted
                    else entry.OriginalValues.SetValues(existing);
                }
            }
        }
    }

    public async Task<int> SaveChangesForUpdateAsync(CancellationToken stoppingToken = default)
    {
        while (true)
        {
            try
            {
                return await SaveChangesAsync(stoppingToken);
            }
            catch (DbUpdateConcurrencyException e)
            {
                foreach (var entry in e.Entries)
                {
                    var existing = await entry.GetDatabaseValuesAsync(stoppingToken);
                    if (existing == null) entry.State = EntityState.Added; // already deleted
                    else entry.OriginalValues.SetValues(existing);
                }
            }
        }
    }

    /// <see>https://stackoverflow.com/questions/74846169/how-bad-are-savepoints-in-postgresql</see>
    /// <see>https://www.cybertec-postgresql.com/en/subtransactions-and-performance-in-postgresql/</see>
    /// <see>https://postgres.ai/blog/20210831-postgresql-subtransactions-considered-harmful#problem-3-unexpected-use-of-multixact-ids</see>
    /// <see>https://about.gitlab.com/blog/2021/09/29/why-we-spent-the-last-month-eliminating-postgresql-subtransactions/</see>
    /// <see>https://gitlab.com/gitlab-org/gitlab/-/issues/338865#note_655312474</see>
    /// <see>https://github.com/dotnet/efcore/issues/23269#issuecomment-2095902588</see>
    protected class NoSavePointTransactionFactory(RelationalTransactionFactoryDependencies dependencies)
        : IRelationalTransactionFactory
    {
        protected virtual RelationalTransactionFactoryDependencies Dependencies { get; } = dependencies;

        public virtual RelationalTransaction Create(
            IRelationalConnection connection,
            DbTransaction transaction,
            Guid transactionId,
            IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger,
            bool transactionOwned)
            => new NoSavePointTransaction(
                connection, transaction, transactionId, logger, transactionOwned, Dependencies.SqlGenerationHelper);

        private sealed class NoSavePointTransaction(IRelationalConnection connection,
            DbTransaction transaction,
            Guid transactionId,
            IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger,
            bool transactionOwned,
            ISqlGenerationHelper sqlGenerationHelper)
            : RelationalTransaction(
                connection, transaction, transactionId, logger, transactionOwned, sqlGenerationHelper)
        {
            public override bool SupportsSavepoints => false;
        }
    }
}
public class TbmDbContext<TModelCacheKeyFactory> : TbmDbContext
    where TModelCacheKeyFactory : class, IModelCacheKeyFactory
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public required IConfiguration Config { private get; init; }
    public DbSet<ImageInReply> ImageInReplies => Set<ImageInReply>();
    public DbSet<ReplyContentImage> ReplyContentImages => Set<ReplyContentImage>();

    [SuppressMessage("Naming", "CA1725:Parameter names should match base declaration")]
    [SuppressMessage("Critical Code Smell", "S927:Parameter names should match base declaration and other partial definitions")]
    [SuppressMessage("Style", "IDE0058:Expression value is never used")]
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseNpgsql(GetNpgsqlDataSource(Config.GetConnectionString("Main")).Value, OnConfiguringNpgsql)
            .ReplaceService<IModelCacheKeyFactory, TModelCacheKeyFactory>()
            .ReplaceService<IRelationalTransactionFactory, NoSavePointTransactionFactory>()
            .UseCamelCaseNamingConvention();

        var dbSettings = Config.GetSection("DbSettings");
#pragma warning disable IDISP004 // Don't ignore created IDisposable
        options.UseLoggerFactory(LoggerFactory.Create(builder =>
            builder.AddNLog(new NLogProviderOptions {RemoveLoggerFactoryFilter = false})
                .SetMinimumLevel((LogLevel)NLog.LogLevel.FromString(
                    dbSettings.GetValue("LogLevel", "Trace")).Ordinal)));
#pragma warning restore IDISP004 // Don't ignore created IDisposable
        if (dbSettings.GetValue("EnableDetailedErrors", false)) options.EnableDetailedErrors();
        if (dbSettings.GetValue("EnableSensitiveDataLogging", false)) options.EnableSensitiveDataLogging();
    }

    [SuppressMessage("Naming", "CA1725:Parameter names should match base declaration")]
    [SuppressMessage("Critical Code Smell", "S927:Parameter names should match base declaration and other partial definitions")]
    [SuppressMessage("Style", "IDE0058:Expression value is never used")]
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ImageInReply>().ToTable("tbmi_imageInReply");
        b.Entity<ReplyContentImage>().HasKey(e => new {e.Pid, e.ImageId});
        b.Entity<ReplyContentImage>().HasOne(e => e.ImageInReply).WithMany();
    }

    protected void OnModelCreatingWithFid(ModelBuilder b, uint fid) =>
        b.Entity<ReplyContentImage>().ToTable($"tbmc_f{fid}_reply_content_image");

    protected virtual void OnConfiguringNpgsql(NpgsqlDbContextOptionsBuilder builder) { }

    protected virtual void OnBuildingNpgsqlDataSource(NpgsqlDataSourceBuilder builder) { }

    protected virtual Lazy<NpgsqlDataSource> GetNpgsqlDataSource(string? connectionString) =>
        throw new NotSupportedException();

    protected Lazy<NpgsqlDataSource> GetNpgsqlDataSourceFactory(string? connectionString) => new(() =>
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        OnBuildingNpgsqlDataSource(dataSourceBuilder);
        return dataSourceBuilder.Build();
    });
}