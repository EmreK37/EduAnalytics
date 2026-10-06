namespace EduAnalytics.Core.Entities;

/// <summary>
/// Ders konusu. Haftalık plana göre tutulur (WeekNumber). Konu ↔ ÖÇ bağı M2M ile.
/// </summary>
public class Topic
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int WeekNumber { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    // Navigation Properties
    public Course Course { get; set; } = null!;
    public ICollection<TopicLearningOutcome> TopicLearningOutcomes { get; set; } = new List<TopicLearningOutcome>();
    public ICollection<QuestionTopic> QuestionTopics { get; set; } = new List<QuestionTopic>();
}
