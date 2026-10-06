using EduAnalytics.Core.Enums;

namespace EduAnalytics.Core.Entities;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation Properties
    public ICollection<Course> Courses { get; set; } = new List<Course>();
    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<QuestionGroup> QuestionGroups { get; set; } = new List<QuestionGroup>();
    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
}
