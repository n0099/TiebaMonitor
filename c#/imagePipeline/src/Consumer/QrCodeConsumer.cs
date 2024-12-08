namespace tbm.ImagePipeline.Consumer;

public sealed class QrCodeConsumer : MatrixConsumer, IDisposable
{
    private readonly FailedImageHandler _failedImageHandler;
    private readonly WeChatQRCode _qrCode;

    public QrCodeConsumer(IConfiguration config, FailedImageHandler failedImageHandler)
    {
        _failedImageHandler = failedImageHandler;
        var modelsPath = config.GetSection("QrCodeConsumer")
            .GetValue("ModelPath", "./OpenCvWechatModels");
        _qrCode = WeChatQRCode.Create(
            $"{modelsPath}/detect.prototxt",
            $"{modelsPath}/detect.caffemodel",
            $"{modelsPath}/sr.prototxt",
            $"{modelsPath}/sr.caffemodel");
    }

    public void Dispose() => _qrCode.Dispose();

    protected override IEnumerable<ImageId> ConsumeInternal(
        ImagePipelineDbContext db,
        IReadOnlyCollection<ImageKeyWithMatrix> imageKeysWithMatrix,
        CancellationToken stoppingToken = default)
    {
        var imageQrCodeEithers = _failedImageHandler
            .TrySelect(imageKeysWithMatrix,
                imageKeyWithMatrix => imageKeyWithMatrix.ImageId,
                ScanQrCodeInImage)
            .ToList();
        db.ImageQrCodes.AddRange(imageQrCodeEithers.Rights().Flatten2());
        return imageQrCodeEithers.Lefts();
    }

    private IEnumerable<ImageQrCode> ScanQrCodeInImage(ImageKeyWithMatrix imageKeyWithMatrix)
    {
        _qrCode.DetectAndDecode(imageKeyWithMatrix.Matrix, out var boxes, out var results);
        return boxes.EquiZip(results)
            .Select(t =>
            {
                var (boxMat, result) = t;
                if (boxMat.Width != 2 || boxMat.Height != 4)
                    throw new InvalidOperationException(
                        $"Unexpected matrix \"{boxMat}\" returned by WeChatQRCode.DetectAndDecode().");
                if (!boxMat.GetArray(out float[] boxPoints))
                    throw new InvalidOperationException("Failed to convert matrix into byte array.");
                var points = boxPoints.Chunk(2).ToList();
                return new ImageQrCode
                {
                    ImageId = imageKeyWithMatrix.ImageId,
                    FrameIndex = imageKeyWithMatrix.FrameIndex,
                    Point1X = points[0][0].RoundToShort(),
                    Point1Y = points[0][1].RoundToShort(),
                    Point2X = points[1][0].RoundToShort(),
                    Point2Y = points[1][1].RoundToShort(),
                    Point3X = points[2][0].RoundToShort(),
                    Point3Y = points[2][1].RoundToShort(),
                    Point4X = points[3][0].RoundToShort(),
                    Point4Y = points[3][1].RoundToShort(),
                    Text = result
                };
            });
    }
}
