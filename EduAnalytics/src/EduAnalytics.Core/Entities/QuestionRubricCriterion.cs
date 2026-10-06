namespace EduAnalytics.Core.Entities;

/// <summary>
/// Klasik (open-ended) soruların değerlendirme kriteri.
/// Bir klasik sorunun toplam puanı = kriterlerin MaxPoints toplamı.
/// Örn: Q11 (10 puan) → "Kavram Doğruluğu" (4p) + "Örnek Verme" (3p) + "Açıklık" (3p)
/// </summary>
public class QuestionRubricCriterion
{
    public int Id { get; set; }
    public int QuestionId { get; set; }

    /// <summary>Kriter başlığı, örn. "Kavram Doğruluğu".</summary>
    public string Title { get; set; } = null!;

    /// <summary>Bu kriterden alınabilecek maksimum puan.</summary>
    public decimal MaxPoints { get; set; }

    /// <summary>Sıralama için.</summary>
    public int Order { get; set; }

    /// <summary>İsteğe bağlı açıklama (notlandırma rehberi).</summary>
    public string? Description { get; set; }

    // Navigation Properties
    public Question Question { get; set; } = null!;
    public ICollection<StudentAnswerCriterion> StudentScores { get; set; } = new List<StudentAnswerCriterion>();
}
