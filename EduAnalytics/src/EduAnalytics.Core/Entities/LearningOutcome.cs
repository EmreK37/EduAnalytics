namespace EduAnalytics.Core.Entities;

/// <summary>
/// Öğrenim Çıktısı (ÖÇ). Her ÖÇ tek bir derse aittir (CourseId zorunlu).
/// Hiyerarşi: ProgramOutcome → LearningOutcome → Topic / Question.
/// </summary>
public class LearningOutcome
{
    public int Id { get; set; }
    public int CourseId { get; set; }

    /// <summary>Ders içindeki ÖÇ kodu, örn. "ÖÇ-1".</summary>
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    // Navigation Properties
    public Course Course { get; set; } = null!;
    public ICollection<TopicLearningOutcome> TopicLearningOutcomes { get; set; } = new List<TopicLearningOutcome>();
    public ICollection<QuestionLearningOutcome> QuestionLearningOutcomes { get; set; } = new List<QuestionLearningOutcome>();
    public ICollection<ProgramOutcomeMapping> ProgramOutcomeMappings { get; set; } = new List<ProgramOutcomeMapping>();
}
