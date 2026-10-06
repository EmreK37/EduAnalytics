namespace EduAnalytics.Core.Entities;

public class StudentCourse
{
    public int StudentId { get; set; }
    public int CourseId { get; set; }

    // Navigation Properties
    public Student Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
}
