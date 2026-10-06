using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Local;

namespace EduAnalytics.Ocr.Reading;

/// <summary>
/// PaddleOCR sarmalayıcısı: formun ad-soyad ve öğrenci no alanlarındaki el yazısını okur.
/// Modeller ilk kullanımda yüklenir (birkaç saniye) ve süreç boyunca bellekte kalır;
/// bu yüzden bu sınıf uygulamada singleton yaşamalıdır. PaddleOcr nesneleri thread-safe
/// olmadığı için tüm çağrılar tek kilit altında serileştirilir.
/// </summary>
public sealed class PaddleTextReader : IDisposable
{
    private readonly object _gate = new();
    private readonly Lazy<PaddleOcrAll> _fullOcr;
    private readonly Lazy<PaddleOcrRecognizer> _recognizer;

    public PaddleTextReader()
    {
        // Latin modeli Türkçe karakterleri (ğ, ş, ı, ö, ü, ç) kapsar.
        FullOcrModel model = LocalFullModels.LatinV3;

        _fullOcr = new Lazy<PaddleOcrAll>(() => new PaddleOcrAll(model, PaddleDevice.Mkldnn())
        {
            AllowRotateDetection = false,
            Enable180Classification = false
        });

        _recognizer = new Lazy<PaddleOcrRecognizer>(
            () => new PaddleOcrRecognizer(model.RecognizationModel, PaddleDevice.Mkldnn()));
    }

    /// <summary>
    /// Serbest el yazısı alanı (ad-soyad): tespit + tanıma birlikte çalışır,
    /// bulunan parçalar soldan sağa birleştirilir.
    /// </summary>
    public (string Text, double Confidence) ReadFreeText(Mat regionGray)
    {
        using var prepared = PrepareForOcr(regionGray);

        lock (_gate)
        {
            PaddleOcrResult result = _fullOcr.Value.Run(prepared);
            var regions = result.Regions
                .Where(r => !string.IsNullOrWhiteSpace(r.Text))
                .OrderBy(r => r.Rect.Center.X)
                .ToList();

            if (regions.Count == 0)
                return (string.Empty, 0);

            var text = string.Join(" ", regions.Select(r => r.Text.Trim()));
            var confidence = regions.Average(r => (double)r.Score);
            return (text, confidence);
        }
    }

    /// <summary>
    /// Tek karakterlik kutucuk (öğrenci no hücresi): tespit adımı atlanır,
    /// doğrudan tanıma çalıştırılır. Dönen metinden ilk harf/rakam alınır.
    /// </summary>
    public (char? Character, double Confidence) ReadCellCharacter(Mat cellGray)
    {
        using var prepared = PrepareForOcr(cellGray);

        lock (_gate)
        {
            PaddleOcrRecognizerResult result = _recognizer.Value.Run(prepared);
            var ch = result.Text?.FirstOrDefault(char.IsLetterOrDigit);
            return ch is null or '\0'
                ? (null, 0)
                : (char.ToUpperInvariant(ch.Value), result.Score);
        }
    }

    /// <summary>Küçük kırpıntıları 2× büyütüp BGR'a çevirir; PaddleOCR küçük görüntüde zayıftır.</summary>
    private static Mat PrepareForOcr(Mat gray)
    {
        var bgr = new Mat();
        Cv2.CvtColor(gray, bgr, ColorConversionCodes.GRAY2BGR);
        if (bgr.Height >= 96)
            return bgr;

        using (bgr)
        {
            var upscaled = new Mat();
            Cv2.Resize(bgr, upscaled, new Size(bgr.Width * 2, bgr.Height * 2),
                interpolation: InterpolationFlags.Cubic);
            return upscaled;
        }
    }

    public void Dispose()
    {
        if (_fullOcr.IsValueCreated)
            _fullOcr.Value.Dispose();
        if (_recognizer.IsValueCreated)
            _recognizer.Value.Dispose();
    }
}
