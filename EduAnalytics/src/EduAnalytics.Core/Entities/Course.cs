namespace EduAnalytics.Core.Entities;

public class Course
{
    public int Id { get; set; }
    public int ProgramId { get; set; }
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string? Description { get; set; }
    public int CreatedByUserId { get; set; }

    // Navigation Properties
    public Program Program { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<Topic> Topics { get; set; } = new List<Topic>();
    public ICollection<LearningOutcome> LearningOutcomes { get; set; } = new List<LearningOutcome>();
    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<QuestionGroup> QuestionGroups { get; set; } = new List<QuestionGroup>();
    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
    public ICollection<StudentCourse> StudentCourses { get; set; } = new List<StudentCourse>();
}
