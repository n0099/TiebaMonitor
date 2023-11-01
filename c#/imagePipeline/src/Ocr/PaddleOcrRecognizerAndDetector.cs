using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Online;

namespace tbm.ImagePipeline.Ocr;

public class PaddleOcrRecognizerAndDetector : IDisposable
{
    private readonly IConfigurationSection _config;
    private readonly string _script;
    private PaddleOcrAll? _ocr;

    public delegate PaddleOcrRecognizerAndDetector New(string script);

    public PaddleOcrRecognizerAndDetector(IConfiguration config, string script)
    {
        _config = config.GetSection("OcrConsumer:PaddleOcr");
        _script = script;
        Settings.GlobalModelDirectory =
            _config.GetValue("ModelPath", "./PaddleOcrModels") ?? "./PaddleOcrModels";
    }

    public void Dispose() => _ocr?.Dispose();

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

    public async Task Initialize(CancellationToken stoppingToken = default) =>
        _ocr ??= await (_script switch
        {
            "zh-Hans" => GetPaddleOcrFactory(OnlineFullModels.ChineseV4),
            "zh-Hant" => GetPaddleOcrFactory(OnlineFullModels.TraditionalChineseV3),
            "ja" => GetPaddleOcrFactory(OnlineFullModels.JapanV4),
            "en" => GetPaddleOcrFactory(OnlineFullModels.EnglishV4),
            _ => throw new ArgumentOutOfRangeException(nameof(_script), _script, "Unsupported script.")
        })(stoppingToken);

    public IEnumerable<PaddleOcrRecognitionResult> RecognizeMatrices
        (Dictionary<ImageKey, Mat> matricesKeyByImageKey, CancellationToken stoppingToken = default)
    {
        Guard.IsNotNull(_ocr);
        return matricesKeyByImageKey.SelectMany(pair =>
        {
            stoppingToken.ThrowIfCancellationRequested();
            return _ocr.Run(pair.Value).Regions.Select(region => new PaddleOcrRecognitionResult(
                pair.Key, region.Rect, region.Text,
                (region.Score * 100).NanToZero().RoundToUshort(),
                _ocr.Recognizer.Model.Version));
        });
    }

    public record DetectionResult(ImageKey ImageKey, RotatedRect TextBox);

    public IEnumerable<DetectionResult> DetectMatrices
        (Dictionary<ImageKey, Mat> matricesKeyByImageKey, CancellationToken stoppingToken = default)
    {
        Guard.IsNotNull(_ocr);
        return matricesKeyByImageKey.SelectMany(pair =>
        {
            stoppingToken.ThrowIfCancellationRequested();
            return _ocr.Detector.Run(pair.Value).Select(rect => new DetectionResult(pair.Key, rect));
        });
    }
}
