using EduAnalytics.Ocr.Models;
using OpenCvSharp;

namespace EduAnalytics.Ocr.Reading;

/// <summary>
/// Okuma hattının orkestratörü:
///   sayfa → hizalama (PageNormalizer) → OMR cevaplar/kitapçık (BubbleReader)
///        → ad-soyad ve öğrenci no (PaddleTextReader).
/// PaddleOCR modelleri süreç boyunca yüklü kaldığı için singleton olarak kaydedilmelidir.
/// </summary>
public sealed class OpticalFormReader : IOpticalFormReader, IDisposable
{
    private readonly PaddleTextReader _textReader = new();

    /// <summary>Bir no hücresinde bu orandan az mürekkep varsa hücre boş sayılır, OCR çağrılmaz.</summary>
    private const double EmptyCellInkRatio = 0.015;

    public bool IsSupportedFile(string path) => PdfPageRasterizer.IsSupported(path);

    public IReadOnlyList<(string SourceLabel, OpticalSheetResult Result)> ReadFile(string path, OpticalFormSpec spec)
    {
        var results = new List<(string, OpticalSheetResult)>();
        var fileName = Path.GetFileName(path);
        var page = 0;

        foreach (var mat in PdfPageRasterizer.LoadPages(path))
        {
            page++;
            using (mat)
            {
                var label = page == 1 && Path.GetExtension(path).ToLowerInvariant() != ".pdf"
                    ? fileName
                    : $"{fileName} (sayfa {page})";
                results.Add((label, ReadSheet(mat, spec)));
            }
        }

        return results;
    }

    public OpticalSheetResult ReadSheet(Mat pageImage, OpticalFormSpec spec)
    {
        var result = new OpticalSheetResult();

        using var normalized = PageNormalizer.Normalize(pageImage);
        if (normalized == null)
        {
            result.MarkersFound = false;
            result.Warnings.Add("Köşe hizalama işaretleri bulunamadı — sayfa optik form olmayabilir veya tarama kalitesi düşük.");
            return result;
        }

        result.MarkersFound = true;

        // Köşe işaretleri 180° dönüşe göre simetriktir: ters beslenen sayfa da "başarıyla"
        // hizalanır. Doğru yönü, baloncuk çemberlerinin şablonla örtüşmesinden seç.
        using var flipped = new Mat();
        Cv2.Rotate(normalized, flipped, RotateFlags.Rotate180);

        var uprightScore = BubbleReader.TemplateAlignmentScore(normalized, spec.QuestionCount);
        var flippedScore = BubbleReader.TemplateAlignmentScore(flipped, spec.QuestionCount);

        var canonical = normalized;
        if (flippedScore > uprightScore)
        {
            canonical = flipped;
            result.Warnings.Add("Sayfa ters (180°) taranmış — otomatik düzeltildi.");
        }

        using var inkMask = BubbleReader.PrepareInkMask(canonical);

        ReadAnswers(inkMask, spec, result);
        ReadBooklet(inkMask, spec, result);
        ReadStudentFields(canonical, inkMask, result);

        return result;
    }

    private static void ReadAnswers(Mat inkMask, OpticalFormSpec spec, OpticalSheetResult result)
    {
        for (var i = 0; i < spec.QuestionCount; i++)
        {
            var answer = BubbleReader.ReadAnswerRow(inkMask, i);
            result.Answers.Add(answer);

            if (answer.MultipleMarks)
                result.Warnings.Add($"Soru {spec.QuestionNumbers[i]}: birden fazla şık işaretlenmiş.");
            else if (answer.WeakMark)
                result.Warnings.Add($"Soru {spec.QuestionNumbers[i]}: silik/kararsız işaret ({answer.Option}).");
        }
    }

    private static void ReadBooklet(Mat inkMask, OpticalFormSpec spec, OpticalSheetResult result)
    {
        if (!spec.HasBookletSection)
            return;

        var (index, confidence, ambiguous) = BubbleReader.ReadBookletRow(inkMask, spec.BookletCodes.Count);
        result.BookletIndex = index;
        result.BookletConfidence = confidence;

        if (index == null)
            result.Warnings.Add(ambiguous
                ? "Kitapçık alanı: birden fazla veya belirsiz işaret."
                : "Kitapçık işaretlenmemiş.");
        else if (ambiguous)
            result.Warnings.Add($"Kitapçık silik işaretlenmiş ({spec.BookletCodes[index.Value]}) — kontrol edin.");
    }

    private void ReadStudentFields(Mat canonical, Mat inkMask, OpticalSheetResult result)
    {
        // Ad-soyad: kutu içi serbest el yazısı → tespit + tanıma.
        using (var nameRegion = CropMm(canonical, OpticalFormTemplate.NameBox, marginMm: 1.0))
        {
            var (text, confidence) = _textReader.ReadFreeText(nameRegion);
            result.RawStudentName = text;
            result.StudentNameConfidence = confidence;
        }

        // Öğrenci no: her kutucuk ayrı kırpılır; boş hücreler OCR'a gönderilmez.
        var digits = new List<char>();
        var confidences = new List<double>();

        for (var i = 0; i < OpticalFormTemplate.StudentNumberCells; i++)
        {
            var cellRect = OpticalFormTemplate.NumberCell(i);

            using var inkCell = CropMm(inkMask, cellRect, marginMm: 1.2);
            var inkRatio = Cv2.CountNonZero(inkCell) / (double)(inkCell.Width * inkCell.Height);
            if (inkRatio < EmptyCellInkRatio)
                continue;

            using var cell = CropMm(canonical, cellRect, marginMm: 1.2);
            var (ch, confidence) = _textReader.ReadCellCharacter(cell);
            if (ch.HasValue)
            {
                digits.Add(ch.Value);
                confidences.Add(confidence);
            }
        }

        result.RawStudentNumber = new string(digits.ToArray());
        result.StudentNumberConfidence = confidences.Count > 0 ? confidences.Average() : 0;

        if (result.RawStudentNumber.Length == 0)
            result.Warnings.Add("Öğrenci numarası okunamadı — elle eşleştirme gerekli.");
        else if (result.StudentNumberConfidence < 0.75)
            result.Warnings.Add($"Öğrenci numarası düşük güvenle okundu: \"{result.RawStudentNumber}\".");
    }

    /// <summary>mm dikdörtgenini kanonik piksellere çevirip içeriden margin kadar daraltarak kırpar
    /// (kutu çizgileri OCR'ı şaşırtmasın diye).</summary>
    private static Mat CropMm(Mat canonical, MmRect rect, double marginMm)
    {
        var scale = OpticalFormTemplate.PxPerMm;
        var px = new Rect(
            (int)Math.Round((rect.X + marginMm) * scale),
            (int)Math.Round((rect.Y + marginMm) * scale),
            (int)Math.Round((rect.Width - 2 * marginMm) * scale),
            (int)Math.Round((rect.Height - 2 * marginMm) * scale));

        px &= new Rect(0, 0, canonical.Width, canonical.Height);
        return new Mat(canonical, px).Clone();
    }

    public void Dispose() => _textReader.Dispose();
}
