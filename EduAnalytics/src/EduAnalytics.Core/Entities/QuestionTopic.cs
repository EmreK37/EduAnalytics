namespace EduAnalytics.Core.Entities;

public class QuestionTopic
{
    public int QuestionId { get; set; }
    public int TopicId { get; set; }

    // Navigation Properties
    public Question Question { get; set; } = null!;
    public Topic Topic { get; set; } = null!;
}
