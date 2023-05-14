using System.Data;

namespace tbm.ImagePipeline.Consumer;

public abstract class ImageBaseConsumer
{
    private readonly ImagePipelineDbContext.New _dbContextFactory;
    private readonly string _script;

    protected ImageBaseConsumer(ImagePipelineDbContext.New dbContextFactory, string script = "")
    {
        _dbContextFactory = dbContextFactory;
        _script = script;
    }

    public async Task Consume(Dictionary<ImageId, Mat> matricesKeyByImageId, CancellationToken stoppingToken)
    {
        try
        {
            var db = _dbContextFactory(_script);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, stoppingToken);
            await ConsumeInternal(db, matricesKeyByImageId, stoppingToken);
            _ = await db.SaveChangesAsync(stoppingToken);
            await transaction.CommitAsync(stoppingToken);
        }
        finally
        {
            matricesKeyByImageId.Values.ForEach(mat => mat.Dispose());
        }
    }

    protected abstract Task ConsumeInternal
        (ImagePipelineDbContext db, Dictionary<ImageId, Mat> matricesKeyByImageId, CancellationToken stoppingToken);
}
