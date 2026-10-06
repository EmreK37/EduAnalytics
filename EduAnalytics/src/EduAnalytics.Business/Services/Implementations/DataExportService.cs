using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using EduAnalytics.Core.Enums;

namespace EduAnalytics.Business.Services.Implementations;

public class DataExportService : IDataExportService
{
    private readonly EduAnalyticsDbContext _context;

    public DataExportService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<AdminExportPackageDto> ExportDatabaseAsync()
    {
        var package = new AdminExportPackageDto
        {
            Version = "1.0",
            Institution = "EduAnalytics Demo Üniversitesi"
        };

        // Export Students
        var students = await _context.Students.ToListAsync();
        package.Students = students.Select(s => new AdminExportStudentDto
        {
            Number = s.StudentNumber,
            FirstName = s.FullName.Split(' ').First(),
            LastName = string.Join(' ', s.FullName.Split(' ').Skip(1)),
            ClassName = s.ClassName
        }).ToList();

        // Export Courses
        var courses = await _context.Courses.ToListAsync();
        package.Courses = courses.Select(c => new AdminExportCourseDto
        {
            Code = c.Code,
            Name = c.Name
        }).ToList();

        // Export Topics
        var topics = await _context.Topics.Include(t => t.Course).ToListAsync();
        package.Topics = topics.Select(t => new AdminExportTopicDto
        {
            CourseCode = t.Course.Code,
            WeekNumber = t.WeekNumber,
            Title = t.Title,
            Description = t.Description
        }).ToList();

        // Export Questions
        var questions = await _context.Questions
            .Include(q => q.Course)
            .Include(q => q.QuestionTopics).ThenInclude(qt => qt.Topic)
            .Include(q => q.RubricCriteria)
            .ToListAsync();

        package.Questions = questions.Select(q => 
        {
            var dto = new AdminExportQuestionDto
            {
                Code = $"Q{q.Id}",
                Content = q.QuestionText,
                Difficulty = 1,
                CourseCode = q.Course.Code,
                TopicTitle = q.QuestionTopics.FirstOrDefault()?.Topic?.Title,
                QuestionType = q.Type.ToString(),
                ClassicAnswerKey = q.AnswerKey,
                ClassicMaxScore = q.MaxPoints,
                RubricCriteria = q.RubricCriteria.Select(r => new AdminExportRubricCriteriaDto
                {
                    Name = r.Title,
                    MaxScore = r.MaxPoints,
                    OrderIndex = r.Order
                }).ToList()
            };

            if (q.Type != QuestionType.OpenEnded)
            {
                dto.Choices = new List<AdminExportQuestionChoiceDto>
                {
                    new AdminExportQuestionChoiceDto { Key = "A", Content = q.OptionA, IsCorrect = q.CorrectOption == OptionLetter.A },
                    new AdminExportQuestionChoiceDto { Key = "B", Content = q.OptionB, IsCorrect = q.CorrectOption == OptionLetter.B },
                    new AdminExportQuestionChoiceDto { Key = "C", Content = q.OptionC, IsCorrect = q.CorrectOption == OptionLetter.C },
                    new AdminExportQuestionChoiceDto { Key = "D", Content = q.OptionD, IsCorrect = q.CorrectOption == OptionLetter.D },
                    new AdminExportQuestionChoiceDto { Key = "E", Content = q.OptionE, IsCorrect = q.CorrectOption == OptionLetter.E }
                }.Where(c => !string.IsNullOrWhiteSpace(c.Content)).ToList();
            }

            return dto;
        }).ToList();

        return package;
    }
}
