using Sdcb.PaddleOCR;

namespace tbm.Crawler.ImagePipeline.Ocr;

public interface IRecognitionResult
{
    public string ImageId { get; }
    public string Script { get; }
    public PaddleOcrResponse.TextBox TextBox { get; }
    public string Text { get; }
    public ushort Confidence { get; }
}

public record PaddleOcrRecognitionResult(string ImageId, string Script,
    PaddleOcrResponse.TextBox TextBox, string Text, ushort Confidence) : IRecognitionResult
{
    public static IEnumerable<PaddleOcrRecognitionResult> FromPaddleSharp(string imageId, string script, PaddleOcrResult result) =>
        result.Regions.Select(region => new PaddleOcrRecognitionResult(
            imageId, script, region.Rect, region.Text, (region.Score * 100).NanToZero().RoundToUshort()));
}

public record TesseractRecognitionResult(string ImageId, string Script, bool IsVertical, bool IsUnrecognized,
    PaddleOcrResponse.TextBox TextBox, string Text, ushort Confidence) : IRecognitionResult;
