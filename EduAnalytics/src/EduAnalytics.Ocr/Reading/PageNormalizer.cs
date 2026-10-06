using OpenCvSharp;

namespace EduAnalytics.Ocr.Reading;

/// <summary>
/// Taranmış sayfayı dört köşedeki dolu kare hizalama işaretlerinden yakalayıp
/// perspektif dönüşümüyle şablonun kanonik çözünürlüğüne (PxPerMm) oturtur.
/// Bu adımdan sonra her baloncuğun piksel koordinatı OpticalFormTemplate'ten
/// doğrudan hesaplanabilir; tarayıcının eğikliği/çözünürlüğü önemsizleşir.
/// </summary>
public static class PageNormalizer
{
    /// <summary>Kanoniğe oturtulmuş gri sayfa döner; işaretler bulunamazsa null.</summary>
    public static Mat? Normalize(Mat src)
    {
        using var gray = ToGray(src);

        using var binary = new Mat();
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

        // Beklenen işaret alanı: taramanın DPI'ını sayfa genişliğinden kestir.
        var dpiEstimate = gray.Width / (OpticalFormTemplate.PageWidthMm / 25.4);
        var expectedSidePx = OpticalFormTemplate.MarkerSizeMm * dpiEstimate / 25.4;
        var expectedArea = expectedSidePx * expectedSidePx;

        var corners = new Point2f[4];
        // Şablondaki sıra: Sol-üst, Sağ-üst, Sol-alt, Sağ-alt
        var quadrants = new[]
        {
            new Rect(0, 0, gray.Width * 3 / 10, gray.Height * 3 / 10),
            new Rect(gray.Width * 7 / 10, 0, gray.Width * 3 / 10, gray.Height * 3 / 10),
            new Rect(0, gray.Height * 7 / 10, gray.Width * 3 / 10, gray.Height * 3 / 10),
            new Rect(gray.Width * 7 / 10, gray.Height * 7 / 10, gray.Width * 3 / 10, gray.Height * 3 / 10)
        };

        for (var i = 0; i < 4; i++)
        {
            var center = FindMarkerCenter(binary, quadrants[i], expectedArea);
            if (center == null)
                return null;
            corners[i] = center.Value;
        }

        var dst = OpticalFormTemplate.MarkerCenters
            .Select(m => new Point2f((float)(m.X * OpticalFormTemplate.PxPerMm),
                                     (float)(m.Y * OpticalFormTemplate.PxPerMm)))
            .ToArray();

        using var transform = Cv2.GetPerspectiveTransform(corners, dst);
        var canonical = new Mat();
        Cv2.WarpPerspective(gray, canonical, transform,
            new Size(OpticalFormTemplate.PageWidthPx, OpticalFormTemplate.PageHeightPx),
            InterpolationFlags.Linear, BorderTypes.Constant, Scalar.White);
        return canonical;
    }

    private static Mat ToGray(Mat src)
    {
        if (src.Channels() == 1)
            return src.Clone();
        var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    /// <summary>Verilen çeyrek bölgede işaret karesine en çok benzeyen konturun merkezini bulur.</summary>
    private static Point2f? FindMarkerCenter(Mat binary, Rect region, double expectedArea)
    {
        using var roi = new Mat(binary, region).Clone();
        Cv2.FindContours(roi, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        Point2f? best = null;
        double bestScore = double.MaxValue;

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < expectedArea * 0.2 || area > expectedArea * 5)
                continue;

            var bounds = Cv2.BoundingRect(contour);
            var aspect = (double)bounds.Width / bounds.Height;
            if (aspect < 0.6 || aspect > 1.7)
                continue;

            // Dolu kare: kontur alanı, çevreleyen dikdörtgeni büyük oranda doldurmalı.
            var fill = area / (bounds.Width * (double)bounds.Height);
            if (fill < 0.7)
                continue;

            var score = Math.Abs(area - expectedArea) / expectedArea;
            if (score < bestScore)
            {
                bestScore = score;
                var m = Cv2.Moments(contour);
                best = new Point2f(
                    (float)(m.M10 / m.M00 + region.X),
                    (float)(m.M01 / m.M00 + region.Y));
            }
        }

        return best;
    }
}
