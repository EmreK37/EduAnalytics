namespace EduAnalytics.Ocr;

/// <summary>
/// Bir sınav için üretilecek/okunacak optik formun içeriği.
/// Formdaki satır sırası = QuestionNumbers sırası; okuma sonucunda aynı sıra kullanılır.
/// </summary>
public class OpticalFormSpec
{
    public string ExamTitle { get; init; } = string.Empty;
    public string CourseName { get; init; } = string.Empty;
    public DateTime? ExamDate { get; init; }

    /// <summary>
    /// Formda basılacak soru numaraları (sınavdaki gerçek numaralar, ör. çoktan seçmeli
    /// olmayan sorular atlanmış olabilir). Uzunluğu formdaki satır sayısını belirler.
    /// </summary>
    public IReadOnlyList<int> QuestionNumbers { get; init; } = Array.Empty<int>();

    /// <summary>Kitapçık kodları (ör. A, B). 1 veya daha az ise kitapçık alanı basılmaz/okunmaz.</summary>
    public IReadOnlyList<string> BookletCodes { get; init; } = Array.Empty<string>();

    public int QuestionCount => QuestionNumbers.Count;
    public bool HasBookletSection => BookletCodes.Count > 1;
}
