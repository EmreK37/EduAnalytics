using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class MyCoursesService : IMyCoursesService
{
    private readonly EduAnalyticsDbContext _context;

    public MyCoursesService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<List<MyCourseDetailDto>> GetMyCoursesAsync()
    {
        var courses = await _context.Courses
            .Include(c => c.StudentCourses)
            .ThenInclude(sc => sc.Student)
            .Include(c => c.Topics)
            .Include(c => c.Questions)
            .ToListAsync();

        var result = new List<MyCourseDetailDto>();
        foreach(var c in courses)
        {
            var dto = new MyCourseDetailDto
            {
                CourseId = c.Id,
                CourseCode = c.Code,
                CourseName = c.Name,
                EnrolledStudentCount = c.StudentCourses.Count,
                TopicCount = c.Topics?.Count ?? 0,
                QuestionCount = c.Questions?.Count ?? 0,
                AverageScore = null, // Can be calculated if exams exist
                EnrolledStudents = c.StudentCourses.Select(sc => new StudentDto
                {
                    Id = sc.Student.Id,
                    StudentNumber = sc.Student.StudentNumber,
                    FullName = sc.Student.FullName,
                    ClassName = sc.Student.ClassName
                }).ToList()
            };
            result.Add(dto);
        }
        return result;
    }

    public async Task<StudentCourseProfileDto> GetStudentProfileAsync(int courseId, int studentId)
    {
        var student = await _context.Students.FindAsync(studentId);
        var course = await _context.Courses.FindAsync(courseId);
        if (student == null || course == null) throw new Exception("Öğrenci veya Ders bulunamadı.");

        var answers = await _context.StudentAnswers
            .Include(a => a.Exam)
            .Include(a => a.Question)
            .Where(a => a.StudentId == studentId && a.Exam.CourseId == courseId)
            .ToListAsync();

        var examsDto = answers.GroupBy(a => a.Exam)
            .Select(g => new StudentExamResultDto
            {
                ExamName = g.Key.Title,
                Date = g.Key.ExamDate,
                Score = g.Sum(a => a.Score ?? (a.IsCorrect ? a.Question.MaxPoints : 0))
            }).ToList();

        return new StudentCourseProfileDto
        {
            StudentId = student.Id,
            FullName = student.FullName,
            StudentNumber = student.StudentNumber,
            ClassName = student.ClassName ?? "Belirtilmemiş",
            CourseName = course.Name,
            Exams = examsDto
        };
    }
}
