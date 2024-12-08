using Sdcb.PaddleInference;
using Sdcb.PaddleOCR.Models.Online;
using static tbm.ImagePipeline.Ocr.PaddleOcrDetector;

namespace tbm.ImagePipeline.Ocr;

public sealed partial class PaddleOcrProvider : IDisposable
{
    private readonly IConfigurationSection _config;
    private readonly string _script;
    private readonly PaddleOcrDetector.New _paddleOcrDetectorFactory;
    private readonly PaddleOcrRecognizer.New _paddleOcrRecognizerFactory;
    private PaddleOcrAll? _ocr;

    public PaddleOcrProvider(IConfiguration config, string script,
        PaddleOcrDetector.New paddleOcrDetectorFactory,
        PaddleOcrRecognizer.New paddleOcrRecognizerFactory)
    {
        _config = config.GetSection("OcrConsumer:PaddleOcr");
        _script = script;
        Settings.GlobalModelDirectory = _config.GetValue("ModelPath", "./PaddleOcrModels");
        _paddleOcrDetectorFactory = paddleOcrDetectorFactory;
        _paddleOcrRecognizerFactory = paddleOcrRecognizerFactory;
    }

    public delegate PaddleOcrProvider New(string script);

    public void Dispose() => _ocr?.Dispose();

    [SuppressMessage("Major Code Smell", "S3928:Parameter names used into ArgumentException constructors should match an existing one ", Justification = "https://github.com/SonarSource/sonar-dotnet/issues/8386#issuecomment-1847872210")]
    [SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public async Task Initialize(CancellationToken stoppingToken = default) =>
        _ocr ??= await (_script switch
        { // https://en.wikipedia.org/wiki/Template:ISO_15924_script_codes_and_related_Unicode_data
            "Hans" => GetPaddleOcrFactory(OnlineFullModels.ChineseV4),
            "Hant" => GetPaddleOcrFactory(OnlineFullModels.TraditionalChineseV3),
            "Jpan" => GetPaddleOcrFactory(OnlineFullModels.JapanV4),
            "Latn" => GetPaddleOcrFactory(OnlineFullModels.EnglishV4),
            _ => throw new ArgumentOutOfRangeException(nameof(_script), _script, "Unsupported script.")
        })(stoppingToken);

    private Func<CancellationToken, Task<PaddleOcrAll>>
        GetPaddleOcrFactory(OnlineFullModels model) => async stoppingToken =>
        new(await model.DownloadAsync(stoppingToken), PaddleDevice.Mkldnn(
            _config.GetValue("MkldnnCacheCapacity", 1), // https://github.com/sdcb/PaddleSharp/pull/46
            _config.GetValue("CpuMathThreadCount", 0),
            _config.GetValue("MemoryOptimized", true),
            _config.GetValue("StdoutLogEnabled", false)))
        {
            AllowRotateDetection = true,
            Enable180Classification = true
        };
}
public sealed partial class PaddleOcrProvider
{
    public IEnumerable<Either<ImageId, IEnumerable<PaddleOcrRecognitionResult>>> RecognizeMatrices(
        IReadOnlyDictionary<ImageKey, Mat> matricesKeyByImageKey,
        FailedImageHandler failedImageHandler,
        CancellationToken stoppingToken = default)
    {
        Guard.IsNotNull(_ocr);
        return _paddleOcrRecognizerFactory(_ocr)
            .RecognizeMatrices(matricesKeyByImageKey, failedImageHandler, stoppingToken);
    }

    public IEnumerable<Either<ImageId, IEnumerable<DetectionResult>>> DetectMatrices(
        IReadOnlyDictionary<ImageKey, Mat> matricesKeyByImageKey,
        FailedImageHandler failedImageHandler,
        CancellationToken stoppingToken = default)
    {
        Guard.IsNotNull(_ocr);
        return _paddleOcrDetectorFactory(_ocr.Detector)
            .DetectMatrices(matricesKeyByImageKey, failedImageHandler, stoppingToken);
    }
}
