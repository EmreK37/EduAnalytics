using EduAnalytics.Core.Enums;
using EduAnalytics.Ocr.Models;
using OpenCvSharp;

namespace EduAnalytics.Ocr.Reading;

/// <summary>
/// OMR: kanonik sayfadaki baloncukların doluluk oranını ölçüp işaretlenen şıkkı belirler.
/// PaddleOCR burada kullanılmaz — bir baloncuğun karalanıp karalanmadığı piksel sayma işidir.
/// </summary>
public static class BubbleReader
{
    /// <summary>Bu oranın üstü kesin dolu kabul edilir.</summary>
    private const double FilledThreshold = 0.40;

    /// <summary>Bu oranın üstü (ama dolu eşiğinin altı) silik işaret sayılır ve incelemeye düşer.</summary>
    private const double WeakThreshold = 0.26;

    /// <summary>
    /// Kanonik gri sayfadan mürekkep maskesi üretir (mürekkep = beyaz piksel).
    /// Erode adımı, baloncukların içine basılı açık gri şık harflerinin ince çizgilerini
    /// siler; kurşun kalem dolgusu bloktur, erode'dan etkilenmez.
    /// </summary>
    public static Mat PrepareInkMask(Mat canonicalGray)
    {
        var ink = new Mat();
        Cv2.AdaptiveThreshold(canonicalGray, ink, 255,
            AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, blockSize: 51, c: 15);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
        Cv2.Erode(ink, ink, kernel, iterations: 1);
        return ink;
    }

    /// <summary>Tek sorunun baloncuk satırını okur (formIndex = formdaki 0-bazlı satır sırası).</summary>
    public static BubbleReadResult ReadAnswerRow(Mat inkMask, int formIndex)
    {
        var ratios = new double[OpticalFormTemplate.OptionCount];
        for (var opt = 0; opt < OpticalFormTemplate.OptionCount; opt++)
            ratios[opt] = FillRatio(inkMask, OpticalFormTemplate.AnswerBubbleCenter(formIndex, opt));

        var result = Decide(ratios, out var optionIndex);
        result.FormIndex = formIndex;
        result.Option = optionIndex >= 0 ? (OptionLetter)(optionIndex + 1) : OptionLetter.Empty;
        return result;
    }

    /// <summary>Kitapçık baloncuklarını okur; işaretli kitapçığın index'ini ve güveni döner.</summary>
    public static (int? Index, double Confidence, bool Ambiguous) ReadBookletRow(Mat inkMask, int bookletCount)
    {
        var ratios = new double[bookletCount];
        for (var i = 0; i < bookletCount; i++)
            ratios[i] = FillRatio(inkMask, OpticalFormTemplate.BookletBubbleCenter(i));

        var decision = Decide(ratios, out var index);
        if (decision.MultipleMarks || index < 0)
            return (null, 0, decision.MultipleMarks || decision.WeakMark);
        return (index, decision.Confidence, decision.WeakMark);
    }

    /// <summary>
    /// Kanonik sayfanın şablonla hizalanma skoru: her cevap baloncuğunun basılı çemberinin
    /// bulunması gereken halka bölgesindeki mürekkep oranlarının ortalaması. Köşe işaretleri
    /// 180° dönüşe göre simetrik olduğundan ters taranmış sayfa da hizalamadan geçer; doğru
    /// yön bu skorla ayırt edilir (ters sayfada baloncuklar çemberlerin üstüne düşmez).
    /// Çember çizgisi ince olduğu için erode'suz ham eşik kullanılır.
    /// </summary>
    public static double TemplateAlignmentScore(Mat canonicalGray, int questionCount)
    {
        using var binary = new Mat();
        Cv2.Threshold(canonicalGray, binary, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

        double sum = 0;
        var samples = 0;
        for (var i = 0; i < questionCount; i++)
        {
            for (var opt = 0; opt < OpticalFormTemplate.OptionCount; opt++)
            {
                sum += AnnulusInkRatio(binary, OpticalFormTemplate.AnswerBubbleCenter(i, opt));
                samples++;
            }
        }
        return samples == 0 ? 0 : sum / samples;
    }

    /// <summary>Baloncuk çemberinin geçtiği halka bölgesindeki (iç–dış yarıçap arası) mürekkep oranı.</summary>
    private static double AnnulusInkRatio(Mat binary, MmPoint centerMm)
    {
        var cx = (int)Math.Round(centerMm.X * OpticalFormTemplate.PxPerMm);
        var cy = (int)Math.Round(centerMm.Y * OpticalFormTemplate.PxPerMm);
        var outer = (int)Math.Round((OpticalFormTemplate.BubbleRadiusMm + 0.35) * OpticalFormTemplate.PxPerMm);
        var inner = (int)Math.Round((OpticalFormTemplate.BubbleRadiusMm - 0.35) * OpticalFormTemplate.PxPerMm);

        var roiRect = new Rect(cx - outer, cy - outer, outer * 2 + 1, outer * 2 + 1);
        roiRect &= new Rect(0, 0, binary.Width, binary.Height);
        if (roiRect.Width <= 0 || roiRect.Height <= 0)
            return 0;

        using var roi = new Mat(binary, roiRect);
        using var ring = new Mat(roiRect.Height, roiRect.Width, MatType.CV_8UC1, Scalar.Black);
        var center = new Point(cx - roiRect.X, cy - roiRect.Y);
        Cv2.Circle(ring, center, outer, Scalar.White, thickness: -1);
        Cv2.Circle(ring, center, inner, Scalar.Black, thickness: -1);

        using var masked = new Mat();
        Cv2.BitwiseAnd(roi, ring, masked);

        var ringArea = Cv2.CountNonZero(ring);
        return ringArea == 0 ? 0 : Cv2.CountNonZero(masked) / (double)ringArea;
    }

    /// <summary>Baloncuğun iç diskindeki mürekkep oranı (0–1). Çemberin kendi çizgisi dışarıda bırakılır.</summary>
    public static double FillRatio(Mat inkMask, MmPoint centerMm)
    {
        var cx = (int)Math.Round(centerMm.X * OpticalFormTemplate.PxPerMm);
        var cy = (int)Math.Round(centerMm.Y * OpticalFormTemplate.PxPerMm);
        var radius = (int)Math.Round((OpticalFormTemplate.BubbleRadiusMm - 0.4) * OpticalFormTemplate.PxPerMm);

        var roiRect = new Rect(cx - radius, cy - radius, radius * 2 + 1, radius * 2 + 1);
        roiRect &= new Rect(0, 0, inkMask.Width, inkMask.Height);
        if (roiRect.Width <= 0 || roiRect.Height <= 0)
            return 0;

        using var roi = new Mat(inkMask, roiRect);
        using var diskMask = new Mat(roiRect.Height, roiRect.Width, MatType.CV_8UC1, Scalar.Black);
        Cv2.Circle(diskMask, new Point(cx - roiRect.X, cy - roiRect.Y), radius, Scalar.White, thickness: -1);

        using var masked = new Mat();
        Cv2.BitwiseAnd(roi, diskMask, masked);

        var diskArea = Cv2.CountNonZero(diskMask);
        return diskArea == 0 ? 0 : Cv2.CountNonZero(masked) / (double)diskArea;
    }

    private static BubbleReadResult Decide(double[] ratios, out int optionIndex)
    {
        var best = -1;
        var second = -1;
        for (var i = 0; i < ratios.Length; i++)
        {
            if (best < 0 || ratios[i] > ratios[best]) { second = best; best = i; }
            else if (second < 0 || ratios[i] > ratios[second]) { second = i; }
        }

        var bestRatio = best >= 0 ? ratios[best] : 0;
        var secondRatio = second >= 0 ? ratios[second] : 0;
        var filledCount = ratios.Count(r => r >= FilledThreshold);

        var result = new BubbleReadResult();

        if (filledCount >= 2)
        {
            // Çift işaret: karar verme, incelemeye bırak.
            result.MultipleMarks = true;
            optionIndex = -1;
            return result;
        }

        if (filledCount == 1)
        {
            optionIndex = best;
            result.Confidence = Math.Clamp((bestRatio - secondRatio) * 2, 0.05, 1);
            return result;
        }

        if (bestRatio >= WeakThreshold)
        {
            // Silik işaret: en olası şıkkı öner ama düşük güvenle işaretle.
            optionIndex = best;
            result.WeakMark = true;
            result.Confidence = Math.Clamp((bestRatio - WeakThreshold) / (FilledThreshold - WeakThreshold) * 0.5, 0.05, 0.5);
            return result;
        }

        // Boş satır.
        optionIndex = -1;
        result.Confidence = Math.Clamp(1 - bestRatio / WeakThreshold, 0, 1);
        return result;
    }
}
