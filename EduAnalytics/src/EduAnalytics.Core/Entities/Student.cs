namespace EduAnalytics.Core.Entities;

public class Student
{
    public int Id { get; set; }
    public string StudentNumber { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string ClassName { get; set; } = null!;

    // Navigation Properties
    public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();
    public ICollection<StudentCourse> StudentCourses { get; set; } = new List<StudentCourse>();
}
