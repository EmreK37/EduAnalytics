namespace EduAnalytics.Core.Entities;

public class QuestionLearningOutcome
{
    public int QuestionId { get; set; }
    public int LearningOutcomeId { get; set; }

    // Navigation Properties
    public Question Question { get; set; } = null!;
    public LearningOutcome LearningOutcome { get; set; } = null!;
}
