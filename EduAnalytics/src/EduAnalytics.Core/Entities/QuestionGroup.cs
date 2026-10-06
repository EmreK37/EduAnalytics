namespace EduAnalytics.Core.Entities;

/// <summary>
/// İlişkili (common-stem) soru grubu. Tek bir gövde (tablo, paragraf, şema) verilir,
/// bu gövdeye bağlı 2+ soru cevaplanır. Örn: bir vaka tablosu + 3 alt soru.
/// </summary>
public class QuestionGroup
{
    public int Id { get; set; }
    public int CourseId { get; set; }

    /// <summary>Gövde metni (paragraf, tablo metni, vakanın açıklaması). Uzun text.</summary>
    public string StemText { get; set; } = null!;

    /// <summary>İsteğe bağlı görsel/şema dosyasının yolu (PNG/JPG/PDF). null = sadece metin.</summary>
    public string? MediaPath { get; set; }

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation Properties
    public Course Course { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
