namespace EduAnalytics.Core.Entities;

/// <summary>
/// Konu (Topic) ↔ Öğrenim Çıktısı (ÖÇ) çoka-çok ilişkisi.
/// Bir konu birden fazla ÖÇ'ye katkı sağlar; bir ÖÇ birden fazla haftada işlenebilir.
/// </summary>
public class TopicLearningOutcome
{
    public int TopicId { get; set; }
    public int LearningOutcomeId { get; set; }

    // Navigation Properties
    public Topic Topic { get; set; } = null!;
    public LearningOutcome LearningOutcome { get; set; } = null!;
}
