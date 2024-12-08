using OpenCvSharp.Text;

namespace tbm.ImagePipeline.Ocr;

public sealed partial class TesseractRecognizer(IConfiguration config, string script) : IDisposable
{
    private readonly IConfigurationSection _config = config.GetSection("OcrConsumer:Tesseract");

    public delegate TesseractRecognizer New(string script);

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [field: AllowNull, MaybeNull]
    private Lazy<OCRTesseract> TesseractInstanceHorizontal => field ??= new(script switch
    { // https://en.wikipedia.org/wiki/Template:ISO_15924_script_codes_and_related_Unicode_data
        "Hans" => CreateTesseract("best/chi_sim+best/eng"),
        "Hant" => CreateTesseract("best/chi_tra+best/eng"),
        "Jpan" => CreateTesseract("best/jpn"), // literal latin letters in japanese is replaced by katakana
        "Latn" => CreateTesseract("best/eng"),
        _ => throw new ArgumentOutOfRangeException(nameof(script), script, "Unsupported script.")
    });

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [field: AllowNull, MaybeNull]
    private Lazy<OCRTesseract> TesseractInstanceVertical => field ??= new(script switch
    {
        "Hans" => CreateTesseract("best/chi_sim_vert", isVertical: true),
        "Hant" => CreateTesseract("best/chi_tra_vert", isVertical: true),
        "Jpan" => CreateTesseract("best/jpn_vert", isVertical: true),
        "Latn" => TesseractInstanceHorizontal.Value, // fallback to best/eng since there's no best/eng_vert
        _ => throw new ArgumentOutOfRangeException(nameof(script), script, "Unsupported script.")
    });

    private int ConfidenceThreshold => _config.GetValue("ConfidenceThreshold", 20);
    private float AspectRatioThresholdToConsiderAsVertical =>
        _config.GetValue("AspectRatioThresholdToConsiderAsVertical", 0.8f);

    public void Dispose()
    {
        TesseractInstanceHorizontal.Value.Dispose();
        TesseractInstanceVertical.Value.Dispose();
    }

    // https://github.com/shimat/opencvsharp/issues/873#issuecomment-1458868153
    // https://pyimagesearch.com/2021/11/15/tesseract-page-segmentation-modes-psms-explained-how-to-improve-your-ocr-accuracy/
    private OCRTesseract CreateTesseract(string scripts, bool isVertical = false) =>
        OCRTesseract.Create(_config.GetValue("DataPath", ""),
            scripts, charWhitelist: "", oem: 1, psmode: isVertical ? 5 : 7);
}
public sealed partial class TesseractRecognizer
{
    public Func<PreprocessedTextBox, TesseractRecognitionResult> RecognizePreprocessedTextBox
        (CancellationToken stoppingToken = default) => textBox =>
    {
        stoppingToken.ThrowIfCancellationRequested();
        var (imageKey, box, preprocessedTextBoxMat) = textBox;
        using var mat = preprocessedTextBoxMat;
        var isVertical = (float)mat.Width / mat.Height < AspectRatioThresholdToConsiderAsVertical;

        // ReSharper disable once StringLiteralTypo
        if (isVertical && script == "Latn") isVertical = false; // there's no vertical latin
        var tesseract = isVertical ? TesseractInstanceVertical : TesseractInstanceHorizontal;
        tesseract.Value.Run(mat, out _, out var rects, out var texts, out var confidences);

        var shouldFallbackToPaddleOcr = rects.Length == 0;
        var components = rects.EquiZip(texts, confidences)
            .Select(t => (Rect: t.Item1, Text: t.Item2, Confidence: t.Item3))
            .Where(t => t.Confidence > ConfidenceThreshold)
            .ToList();
        var text = string.Concat(components.Select(t => t.Text)).Trim();
        if (text == "") shouldFallbackToPaddleOcr = true;
        var averageConfidence = components.Count != 0
            ? components.Average(c => c.Confidence).RoundToByte()
            : (byte)0;

        return new(imageKey, box, text, averageConfidence, isVertical, shouldFallbackToPaddleOcr);
    };
}
public sealed partial class TesseractRecognizer
{
    public static IEnumerable<PreprocessedTextBox> PreprocessTextBoxes(
        ImageKey imageKey,
        Mat originalMatrix,
        IEnumerable<RotatedRect> textBoxes,
        CancellationToken stoppingToken = default) => textBoxes.Select(textBox =>
    {
        stoppingToken.ThrowIfCancellationRequested();

        // not using RotatedRect.Angle directly since it's not based on a stable order of four vertices
        var degrees = GetRotationDegrees(textBox); // https://github.com/opencv/opencv/issues/23335

#pragma warning disable IDISP001 // Dispose created
        // crop by circumscribed rectangle, intersect will prevent textBox outside originalMatrix
        var mat = new Mat(originalMatrix, new Rect(default, originalMatrix.Size()).Intersect(textBox.BoundingRect()));
#pragma warning restore IDISP001 // Dispose created

        if (mat.Channels() != 1) Cv2.CvtColor(mat, mat, ColorConversionCodes.BGR2GRAY);

        // https://docs.opencv.org/4.7.0/d7/d4d/tutorial_py_thresholding.html
        // http://www.fmwconcepts.com/imagemagick/threshold_comparison/index.php
        _ = Cv2.Threshold(mat, mat, thresh: 0, maxval: 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);

        // https://stackoverflow.com/questions/9392869/where-do-i-find-the-machine-epsilon-in-c
        if (MathF.Abs(degrees) < MathF.Pow(2, -24)) RotateMatrix(mat, degrees);

        // https://github.com/tesseract-ocr/tesseract/issues/427
        Cv2.CopyMakeBorder(mat, mat, 10, 10, 10, 10, BorderTypes.Constant, new(0, 0, 0));

        // https://github.com/tesseract-ocr/tesseract/issues/3001
        return mat.Width < 3 ? null : new PreprocessedTextBox(imageKey, textBox, mat);
    }).OfType<PreprocessedTextBox>();

    private static float GetRotationDegrees(RotatedRect rotatedRect)
    { // https://stackoverflow.com/questions/13002979/how-to-calculate-rotation-angle-from-rectangle-points
        var (topLeft, topRight, bottomLeft, bottomRight) = rotatedRect.GetPoints();
        if (topLeft.X == bottomLeft.X
            && topRight.X == bottomRight.X
            && topLeft.Y == topRight.Y
            && bottomLeft.Y == bottomRight.Y) return 0;
        var xAxisDiff = bottomLeft.X - topLeft.X;
        var yAxisDiff = bottomLeft.Y - topLeft.Y;

        // atan2(y,x) is the radians of the angle C in a right triangle with side b=4 (xAxisDiff) and side c=1 (yAxisDiff)
        // https://www.calculator.net/triangle-calculator.html?vc=&vx=4&vy=&va=90&vz=1&vb=&angleunits=d&x=53&y=29
        return (float)double.RadiansToDegrees(Math.Atan2(xAxisDiff, yAxisDiff));
    }

    private static void RotateMatrix(Mat src, float degrees)
    { // https://stackoverflow.com/questions/22041699/rotate-an-image-without-cropping-in-opencv-in-c/75451191#75451191
        degrees = -degrees; // counter-clockwise to clockwise
        var center = new Point2f((src.Width - 1) / 2f, (src.Height - 1) / 2f);
        using var rotationMat = Cv2.GetRotationMatrix2D(center, degrees, scale: 1);
        var boundingRect = new RotatedRect(default, new(src.Width, src.Height), degrees).BoundingRect();
        rotationMat.Set(0, 2, rotationMat.Get<double>(0, 2) + (boundingRect.Width / 2f) - (src.Width / 2f));
        rotationMat.Set(1, 2, rotationMat.Get<double>(1, 2) + (boundingRect.Height / 2f) - (src.Height / 2f));

        // https://stackoverflow.com/questions/39371507/image-loses-quality-with-cv2-warpperspective
        Cv2.WarpAffine(src, src, rotationMat, boundingRect.Size, InterpolationFlags.Nearest);
    }

    public record PreprocessedTextBox(ImageKey ImageKey, RotatedRect TextBox, Mat Mat);
}
