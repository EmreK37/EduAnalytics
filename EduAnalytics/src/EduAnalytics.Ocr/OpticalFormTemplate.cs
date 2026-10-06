namespace EduAnalytics.Ocr;

public readonly record struct MmPoint(double X, double Y);

public readonly record struct MmRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

/// <summary>
/// Optik formun mm cinsinden geometrisi. PDF üretici ve okuyucu AYNI sabitleri kullanır;
/// form düzeni değişecekse yalnızca burası değişmelidir. Sayfa A4 (210×297 mm),
/// koordinatlar sol üst köşeden ölçülür.
/// </summary>
public static class OpticalFormTemplate
{
    public const double PageWidthMm = 210;
    public const double PageHeightMm = 297;

    /// <summary>Okuyucunun homografi sonrası çalıştığı kanonik çözünürlük (~203 DPI).</summary>
    public const double PxPerMm = 8;

    public static int PageWidthPx => (int)(PageWidthMm * PxPerMm);
    public static int PageHeightPx => (int)(PageHeightMm * PxPerMm);

    // ── Köşe hizalama işaretleri (içi dolu kare) ──
    public const double MarkerSizeMm = 6;

    /// <summary>Sıra: Sol-üst, Sağ-üst, Sol-alt, Sağ-alt (kare merkezleri).</summary>
    public static readonly MmPoint[] MarkerCenters =
    {
        new(12, 12),
        new(198, 12),
        new(12, 285),
        new(198, 285)
    };

    // ── Kapasite ──
    public const int MaxQuestions = 100;
    public const int QuestionsPerColumn = 25;
    public const int OptionCount = 5;          // form her zaman A–E basar
    public const int StudentNumberCells = 12;  // öğrenci no kutucuk sayısı
    public const int MaxBooklets = 5;

    // ── Bölüm 1: PaddleOCR ile okunan alanlar ──
    /// <summary>Ad-soyad yazma kutusu (serbest el yazısı).</summary>
    public static readonly MmRect NameBox = new(15, 46, 110, 12);

    /// <summary>Öğrenci no kutucuk şeridi; her karakter ayrı hücreye yazılır.</summary>
    public static readonly MmRect NumberRow = new(15, 64, NumberCellWidthMm * StudentNumberCells, 12);
    public const double NumberCellWidthMm = 9;

    public static MmRect NumberCell(int index)
        => new(NumberRow.X + index * NumberCellWidthMm, NumberRow.Y, NumberCellWidthMm, NumberRow.Height);

    // ── Kitapçık baloncukları (OMR) ──
    public const double BookletBubbleFirstX = 148;
    public const double BookletBubblePitch = 9;
    public const double BookletBubbleY = 70;

    public static MmPoint BookletBubbleCenter(int bookletIndex)
        => new(BookletBubbleFirstX + bookletIndex * BookletBubblePitch, BookletBubbleY);

    // ── Bölüm 2: Cevap baloncukları (OMR) ──
    public const double BubbleRadiusMm = 1.8;
    public const double AnswerFirstRowY = 90;      // ilk satırın baloncuk merkezi
    public const double AnswerRowPitch = 7.5;
    public const double AnswerBubbleFirstOffsetX = 13; // kolon başlangıcı → ilk şık merkezi
    public const double AnswerOptionPitch = 7;
    public const double QuestionNumberOffsetX = 7;     // soru numarası metninin sağ hizası

    /// <summary>Her sütunun sol başlangıç x'i. 4 sütun × 25 soru = 100 soru kapasitesi.</summary>
    public static readonly double[] ColumnOriginsX = { 15, 62, 109, 156 };

    /// <summary>Formdaki sıra (0-bazlı) → sütun/satır konumu.</summary>
    public static (int Column, int Row) FormPosition(int formIndex)
        => (formIndex / QuestionsPerColumn, formIndex % QuestionsPerColumn);

    public static MmPoint AnswerBubbleCenter(int formIndex, int optionIndex)
    {
        var (col, row) = FormPosition(formIndex);
        return new MmPoint(
            ColumnOriginsX[col] + AnswerBubbleFirstOffsetX + optionIndex * AnswerOptionPitch,
            AnswerFirstRowY + row * AnswerRowPitch);
    }
}
