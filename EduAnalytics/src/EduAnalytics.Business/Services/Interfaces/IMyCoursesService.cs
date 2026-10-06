using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

public class MyCourseDetailDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int EnrolledStudentCount { get; set; }
    public int TopicCount { get; set; }
    public int QuestionCount { get; set; }
    public decimal? AverageScore { get; set; }
    public List<StudentDto> EnrolledStudents { get; set; } = new();
}

public class StudentCourseProfileDto
{
    public int StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string StudentNumber { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public List<StudentExamResultDto> Exams { get; set; } = new();
}

public class StudentExamResultDto
{
    public string ExamName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Score { get; set; }
}

public interface IMyCoursesService
{
    Task<List<MyCourseDetailDto>> GetMyCoursesAsync();
    Task<StudentCourseProfileDto> GetStudentProfileAsync(int courseId, int studentId);
}
