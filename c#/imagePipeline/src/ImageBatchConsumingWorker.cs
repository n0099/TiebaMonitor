using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using SixLabors.ImageSharp.PixelFormats;

namespace tbm.ImagePipeline;

public class ImageBatchConsumingWorker(
    ILogger<ImageBatchConsumingWorker> logger,
    ILifetimeScope scope0,

    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    Channel<List<ImageWithBytes>> channel,
    Func<Owned<ImagePipelineDbContext.New>> dbContextFactory,
    Func<Owned<ImagePipelineDbContext.NewDefault>> dbContextDefaultFactory)
    : ErrorableWorker(shouldExitOnException: true, shouldExitOnFinish: true)
{
    protected override async Task DoWork(CancellationToken stoppingToken)
    {
        await foreach (var imagesWithBytes in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await Consume(imagesWithBytes, stoppingToken);
            }
            catch (OperationCanceledException e) when (e.CancellationToken == stoppingToken)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Exception");
            }
            if (!(channel.Reader.Completion.IsCompleted || channel.Reader.TryPeek(out _)))

                // https://stackoverflow.com/questions/72972469/which-is-the-fastest-way-to-tell-if-a-channelt-is-empty
                logger.LogWarning("Consumer is idle due to no image batch getting produced, configure \"ImageBatchProducer.MaxBufferedImageBatches\""
                                  + " to a larger value then restart will allow more image batches to get downloaded before consume");
        }
        logger.LogInformation("No more image batch to consume, configure \"ImageBatchProducer.StartFromLatestSuccessful\""
                              + " to false then restart will rerun all previous failed image batches from start");
    }

    private async Task Consume(
        List<ImageWithBytes> imagesWithBytes,
        CancellationToken stoppingToken = default)
    {
        await using var dbFactory = dbContextDefaultFactory();
        var db = dbFactory.Value();
        await using var transaction = await db.Database.BeginTransactionAsync
            (IsolationLevel.ReadCommitted, stoppingToken);

        await using var scope1 = scope0.BeginLifetimeScope(builder =>
            builder.RegisterType<FailedImageHandler>()
                .WithParameter(new NamedParameter("stoppingToken", stoppingToken))
                .SingleInstance()); // only in this and nested scopes
        var failedImageHandler = scope1.Resolve<FailedImageHandler>();

        // these components requiring the singleton of FailedImageHandler in current scope
        // each Func<Owned<T>> representing a factory Func<> to create new scope Owned<> to resolve component T
        // https://autofac.readthedocs.io/en/latest/advanced/owned-instances.html
        var metadataConsumerFactory = scope1.Resolve<Func<Owned<MetadataConsumer>>>();
        var hashConsumerFactory = scope1.Resolve<Func<Owned<HashConsumer>>>();
        var qrCodeConsumerFactory = scope1.Resolve<Func<Owned<QrCodeConsumer>>>();
        var ocrConsumerFactory = scope1.Resolve<Func<Owned<OcrConsumer.New>>>();

        var imagesInReply = imagesWithBytes.Select(i => i.ImageInReply).ToList();
        db.AttachRange(imagesInReply);
        void MarkImagesInReplyAsConsumed
            (Expression<Func<ImageInReply, bool>> selector, IEnumerable<ImageId> imagesIdToUpdate) =>
            db.ChangeTracker.Entries<ImageInReply>()
                .IntersectBy(imagesInReply
                    .IntersectBy(imagesIdToUpdate, i => i.ImageId), entry => entry.Entity)
                .ForEach(entry => entry.Property(selector).CurrentValue = true);

        logger.LogTrace("Start to consume {} image(s): [{}]",
            imagesWithBytes.Count, string.Join(',', imagesInReply.Select(i => i.ImageId)));
        var stopwatch = new Stopwatch();
        void LogStopwatch(string consumerType, IReadOnlyCollection<ImageId> imagesId) =>
            logger.LogTrace("Spend {}ms to {} for {} image(s): [{}]",
                stopwatch.ElapsedMilliseconds, consumerType, imagesId.Count, string.Join(',', imagesId));

        void ConsumeConsumer<TImage, TConsumer>(
            Expression<Func<ImageInReply, bool>> selector, IReadOnlyCollection<TImage> images,
            Func<Owned<TConsumer>> consumerFactory, string consumerType)
            where TConsumer : IConsumer<TImage>
        {
            using var consumer = consumerFactory();
            stopwatch.Restart();
#pragma warning disable IDE0042 // Deconstruct variable declaration
            var imagesId = consumer.Value.Consume(db, images, stoppingToken);
#pragma warning restore IDE0042 // Deconstruct variable declaration
            var failed = imagesId.Failed.ToList();
            var consumed = imagesId.Consumed.ToList();
            LogStopwatch(consumerType, [.. consumed, .. failed]);
            MarkImagesInReplyAsConsumed(selector, consumed);

            if (failed.Count == 0) return;
            logger.LogError("Failed to {} for {} image(s): [{}]",
                consumerType, failed.Count, string.Join(',', failed));
        }

        ConsumeConsumer(i => i.MetadataConsumed,
            imagesWithBytes.Where(i => !i.ImageInReply.MetadataConsumed).ToList(),
            metadataConsumerFactory, "extract metadata");

        var imageKeysWithMatrix = failedImageHandler.TrySelect(imagesWithBytes
                    .Where(i => i.ImageInReply is not {HashConsumed: true, QrCodeConsumed: true, OcrConsumed: true}),
                imageWithBytes => imageWithBytes.ImageInReply.ImageId,
                DecodeImageOrFramesBytes(stoppingToken))
            .Rights().Flatten2().ToList();
        try
        {
            IReadOnlyCollection<ImageKeyWithMatrix> ExceptConsumed
                (Func<ImageInReply, bool> selector) => imageKeysWithMatrix
                .ExceptBy(imagesInReply.Where(selector)
                    .Select(i => i.ImageId), i => i.ImageId)
                .ToList();
            ConsumeConsumer(i => i.HashConsumed,
                ExceptConsumed(i => i.HashConsumed),
                hashConsumerFactory, "calculate hash");
            ConsumeConsumer(i => i.QrCodeConsumed,
                ExceptConsumed(i => i.QrCodeConsumed),
                qrCodeConsumerFactory, "scan QRCode");
            await ConsumeOcrConsumer(consumedImagesId =>
                    MarkImagesInReplyAsConsumed(i => i.OcrConsumed, consumedImagesId),
                [.. ExceptConsumed(i => i.OcrConsumed)],
                ocrConsumerFactory, db.Database.GetDbConnection(),
                transaction.GetDbTransaction(), db.ForumScripts, stoppingToken);
        }
        finally
        {
            imageKeysWithMatrix.ForEach(i => i.Matrix.Dispose());
        }

        failedImageHandler.Save(db);
        _ = await db.SaveChangesAsync(stoppingToken); // https://github.com/dotnet/EntityFramework.Docs/pull/4358
        await transaction.CommitAsync(stoppingToken);
    }

    private Func<ImageWithBytes, IEnumerable<ImageKeyWithMatrix>> DecodeImageOrFramesBytes
        (CancellationToken stoppingToken = default) => imageWithBytes =>
    {
        var imageBytes = imageWithBytes.Bytes;
        var imageId = imageWithBytes.ImageInReply.ImageId;

        // preserve alpha channel if there's any, so the type of mat might be CV_8UC3 or CV_8UC4
        var imageMat = Cv2.ImDecode(imageBytes, ImreadModes.Unchanged);
        if (!imageMat.Empty())
            return [new(imageId, FrameIndex: 0, imageMat)];

        ImageKeyWithMatrix DecodeFrame(ImageFrame<Rgb24> frame, int frameIndex)
        {
            stoppingToken.ThrowIfCancellationRequested();
            var frameBytes = new Rgb24[frame.Width * frame.Height];
            frame.CopyPixelDataTo(frameBytes);
            using var frameImage = Image.LoadPixelData<Rgb24>(frameBytes, frame.Width, frame.Height);
            using var stream = new MemoryStream();
            frameImage.SaveAsPng(stream);
            ObjectDisposedException.ThrowIf(!stream.TryGetBuffer(out var buffer), stream);
#pragma warning disable IDISP001 // Dispose created
            var frameMat = Cv2.ImDecode(buffer, ImreadModes.Unchanged);
#pragma warning restore IDISP001 // Dispose created
            return frameMat.Empty()
                ? throw new InvalidOperationException(
                    $"Failed to decode frame {frameIndex} of image {imageId}.")
                : new(imageId, (uint)frameIndex, frameMat);
        }

        try
        {
            if (Image.DetectFormat(imageBytes) is GifFormat)
            {
                using var image = Image.Load<Rgb24>(imageBytes);
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var ret = image.Frames.AsEnumerable().Select(DecodeFrame).ToList();
                logger.LogTrace("Spending {}ms to Extracted {} frames out of GIF image {}",
                    stopwatch.ElapsedMilliseconds, image.Frames.Count, imageId);
                return ret;
            }
            throw new NotSupportedException($"Image {imageId} cannot decode by OpenCV and is not GIF format.");
        }
        finally
        {
            imageMat.Dispose();
        }
    };

    private async Task ConsumeOcrConsumer(
        Action<IEnumerable<ImageId>> markImageInReplyAsConsumed,
        IReadOnlyCollection<ImageKeyWithMatrix> imageKeysWithMatrix,
        Func<Owned<OcrConsumer.New>> ocrConsumerFactory,
        DbConnection parentConnection,
        DbTransaction parentTransaction,
        IQueryable<ForumScript> forumScripts,
        CancellationToken stoppingToken = default)
    {
        var scriptGroupings = await forumScripts.AsNoTracking()
            .GroupBy(e => e.Fid, e => e.Script)
            .ToListAsync(stoppingToken);
        var scripts = scriptGroupings.Flatten2().Distinct().ToList();
        var recognizedTextLinesKeyByScript = new Dictionary<string, List<ImageOcrLine>>(scripts.Count);
        scripts.ForEach(script => recognizedTextLinesKeyByScript[script] = []);

        foreach (var scriptsGroupByFid in scriptGroupings)
        {
            var fid = scriptsGroupByFid.Key;
            IEnumerable<ImageKeyWithMatrix> GetImagesInCurrentFid()
            { // dispose the scope of Owned<DbContext> after return to prevent long-life idle connection
                using var dbFactory = dbContextFactory();
                var db = dbFactory.Value(fid, "");
                db.Database.SetDbConnection(parentConnection);
#pragma warning disable IDISP004 // Don't ignore created IDisposable
                _ = db.Database.UseTransaction(parentTransaction);
#pragma warning restore IDISP004 // Don't ignore created IDisposable

                // try to know which fid owns current image batch
                return imageKeysWithMatrix.IntersectBy(
                    from replyContentImage in db.ReplyContentImages.AsNoTracking()
                    where imageKeysWithMatrix
                        .Select(imageKeyWithMatrix => imageKeyWithMatrix.ImageId)
                        .Contains(replyContentImage.ImageId)
                    select replyContentImage.ImageId,
                    imageKeyWithMatrix => imageKeyWithMatrix.ImageId);
            }

            var imagesInCurrentFid = GetImagesInCurrentFid().ToList();
            if (imagesInCurrentFid.Count == 0) continue;
            foreach (var script in scriptsGroupByFid)
            {
                await using var dbFactory = dbContextFactory();
                var db = dbFactory.Value(fid, script);

                // https://learn.microsoft.com/en-us/ef/core/saving/transactions#share-connection-and-transaction
                db.Database.SetDbConnection(parentConnection);
                _ = await db.Database.UseTransactionAsync(parentTransaction, stoppingToken);
                var recognizedTextLines = recognizedTextLinesKeyByScript[script];

                // exclude images have already been recognized for current script
                // due to being referenced across multiple forums
                var uniqueImagesInCurrentFid = imagesInCurrentFid
                    .ExceptBy(recognizedTextLines.Select(i => i.ImageId), i => i.ImageId).ToList();

                // insert their previously recognized lines into the table of current forum and script
                await db.ImageOcrLines.AddRangeAsync(recognizedTextLines.IntersectBy(
                    imagesInCurrentFid.Except(uniqueImagesInCurrentFid).Select(i => i.ImageId),
                    i => i.ImageId), stoppingToken);
                recognizedTextLines.AddRange(await ConsumeByFidWithScript(db, fid, script, uniqueImagesInCurrentFid));
                _ = await db.SaveChangesAsync(stoppingToken);
            }
        }

        async Task<IEnumerable<ImageOcrLine>> ConsumeByFidWithScript(
            ImagePipelineDbContext db,
            Fid fid,
            string script,
            IReadOnlyCollection<ImageKeyWithMatrix> imagesInCurrentFid)
        {
            await using var consumerFactory = ocrConsumerFactory();
            var ocrConsumer = consumerFactory.Value(script);
            await ocrConsumer.InitializePaddleOcr(stoppingToken);

            var stopwatch = new Stopwatch();
            stopwatch.Start();
#pragma warning disable IDE0042 // Deconstruct variable declaration
            var imagesId = ocrConsumer.Consume(db, imagesInCurrentFid, stoppingToken);
#pragma warning restore IDE0042 // Deconstruct variable declaration
            stopwatch.Stop();
            markImageInReplyAsConsumed(imagesId.Consumed);

            var failed = imagesId.Failed.ToList();
            if (failed.Count != 0)
                logger.LogError("Failed to detect and recognize {} script text for fid {} in {} image(s): [{}]",
                    script, fid, failed.Count, string.Join(',', failed));
            logger.LogTrace("Spend {}ms to detect and recognize {} script text for fid {} in {} image(s): [{}]",
                stopwatch.ElapsedMilliseconds, script, fid, imagesInCurrentFid.Count,
                string.Join(',', imagesInCurrentFid.Select(i => i.ImageId)));

            return ocrConsumer.RecognizedTextLines;
        }
    }
}
