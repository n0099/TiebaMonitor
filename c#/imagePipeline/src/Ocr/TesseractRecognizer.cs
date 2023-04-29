using OpenCvSharp.Text;

namespace tbm.ImagePipeline.Ocr;

public class TesseractRecognizer : IDisposable
{
    private readonly string _script;
    private readonly OCRTesseract _tesseractInstanceHorizontal;
    private readonly OCRTesseract _tesseractInstanceVertical;
    private readonly int _confidenceThreshold;
    private readonly float _aspectRatioThresholdToConsiderAsVertical;

    public delegate TesseractRecognizer New(string script);

    public TesseractRecognizer(IConfiguration config, string script)
    {
        _script = script;
        var configSection = config.GetSection("ImageOcrPipeline").GetSection("Tesseract");
        var dataPath = configSection.GetValue("DataPath", "") ?? "";
        OCRTesseract CreateTesseract(string scripts, int psmode) =>
            // https://github.com/shimat/opencvsharp/issues/873#issuecomment-1458868153
            OCRTesseract.Create(dataPath, scripts, "", 1, psmode);
        _tesseractInstanceHorizontal = script switch
        { // https://pyimagesearch.com/2021/11/15/tesseract-page-segmentation-modes-psms-explained-how-to-improve-your-ocr-accuracy/
            "zh-Hans" => CreateTesseract("best/chi_sim+best/eng", 7),
            "zh-Hant" => CreateTesseract("best/chi_tra+best/eng", 7),
            "ja" => CreateTesseract("best/jpn", 7), // literal latin letters in japanese is replaced by katakana
            "en" => CreateTesseract("best/eng", 7),
            _ => throw new ArgumentOutOfRangeException(nameof(script), script, "Unsupported script.")
        };
        _tesseractInstanceVertical = script switch
        {
            "zh-Hans" => CreateTesseract("best/chi_sim_vert", 5),
            "zh-Hant" => CreateTesseract("best/chi_tra_vert", 5),
            "ja" => CreateTesseract("best/jpn_vert", 5),
            "en" => _tesseractInstanceHorizontal, // fallback to best/eng since there's no best/eng_vert
            _ => throw new ArgumentOutOfRangeException(nameof(script), script, "Unsupported script.")
        };
        _confidenceThreshold = configSection.GetValue("ConfidenceThreshold", 20);
        _aspectRatioThresholdToConsiderAsVertical = configSection.GetValue("AspectRatioThresholdToConsiderAsVertical", 0.8f);
    }

    public void Dispose()
    {
        _tesseractInstanceHorizontal.Dispose();
        _tesseractInstanceVertical.Dispose();
    }

    public record PreprocessedTextBox(ImageId ImageId, bool IsUnrecognized, RotatedRect TextBox, Mat PreprocessedTextBoxMat);

    public static IEnumerable<PreprocessedTextBox> PreprocessTextBoxes
        (ImageId imageId, Mat originalImageMat, IEnumerable<(bool IsUnrecognized, RotatedRect)> textBoxes) => textBoxes
        .Select(t =>
        {
            var (isUnrecognized, textBox) = t;
            // not using RotatedRect.Angle directly since it's not based on a stable order of four vertices
            var degrees = GetRotationDegrees(textBox); // https://github.com/opencv/opencv/issues/23335
            // crop by circumscribed rectangle, intersect will prevent textBox outside originalImageMat
            var mat = new Mat(originalImageMat, new Rect(new(), originalImageMat.Size()).Intersect(textBox.BoundingRect()));

            Cv2.CvtColor(mat, mat, ColorConversionCodes.BGR2GRAY);
            // https://docs.opencv.org/4.7.0/d7/d4d/tutorial_py_thresholding.html
            // http://www.fmwconcepts.com/imagemagick/threshold_comparison/index.php
            _ = Cv2.Threshold(mat, mat, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);

            if (degrees != 0) RotateMatrix(mat, degrees);

            // https://github.com/tesseract-ocr/tesseract/issues/427
            Cv2.CopyMakeBorder(mat, mat, 10, 10, 10, 10, BorderTypes.Constant, new(0, 0, 0));

            // https://github.com/tesseract-ocr/tesseract/issues/3001
            return mat.Width < 3 ? null : new PreprocessedTextBox(imageId, isUnrecognized, textBox, mat);
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
        // https://www.calculator.net/triangle-calculator.html?vc=&vx=4&vy=&va=90&vz=1&vb=&angleunits=d&x=53&y=29
        return (float)(Math.Atan2(xAxisDiff, yAxisDiff) * 180 / Math.PI); // radians to degrees
    }

    private static void RotateMatrix(Mat src, float degrees)
    { // https://stackoverflow.com/questions/22041699/rotate-an-image-without-cropping-in-opencv-in-c/75451191#75451191
        degrees = -degrees; // counter-clockwise to clockwise
        var center = new Point2f((src.Width - 1) / 2f, (src.Height - 1) / 2f);
        using var rotationMat = Cv2.GetRotationMatrix2D(center, degrees, 1);
        var boundingRect = new RotatedRect(new(), new(src.Width, src.Height), degrees).BoundingRect();
        rotationMat.Set(0, 2, rotationMat.Get<double>(0, 2) + (boundingRect.Width / 2f) - (src.Width / 2f));
        rotationMat.Set(1, 2, rotationMat.Get<double>(1, 2) + (boundingRect.Height / 2f) - (src.Height / 2f));
        Cv2.WarpAffine(src, src, rotationMat, boundingRect.Size);
    }

    public TesseractRecognitionResult RecognizePreprocessedTextBox(PreprocessedTextBox textBox)
    {
        var (imageId, isUnrecognized, box, preprocessedTextBoxMat) = textBox;
        using var mat = preprocessedTextBoxMat;
        var isVertical = (float)mat.Width / mat.Height < _aspectRatioThresholdToConsiderAsVertical;
        if (isVertical && _script == "en") isVertical = false; // there's no vertical english
        var tesseract = isVertical ? _tesseractInstanceVertical : _tesseractInstanceHorizontal;
        tesseract.Run(mat, out _, out var rects, out var texts, out var confidences);

        var shouldFallbackToPaddleOcr = !rects.Any();
        var components = rects.Zip(texts, confidences)
            .Select(t => (Rect: t.First, Text: t.Second, Confidence: t.Third))
            .Where(t => t.Confidence > _confidenceThreshold)
            .ToList();
        var text = string.Join("", components.Select(t => t.Text)).Trim();
        if (text == "") shouldFallbackToPaddleOcr = true;
        var averageConfidence = components.Any()
            ? components.Select(c => c.Confidence).Average().RoundToUshort()
            : (ushort)0;

        return new(imageId, _script, box, text, averageConfidence, isVertical, isUnrecognized, shouldFallbackToPaddleOcr);
    }
}
