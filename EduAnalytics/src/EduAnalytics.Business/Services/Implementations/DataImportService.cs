using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.DataAccess.Context;
using EduAnalytics.Core.Entities;
using EduAnalytics.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class DataImportService : IDataImportService
{
    private readonly EduAnalyticsDbContext _context;

    public DataImportService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<(int CoursesAdded, int StudentsAdded, int TopicsAdded, int QuestionsAdded)> ImportAdminPackageAsync(AdminExportPackageDto package)
    {
        if (package == null) throw new ArgumentNullException(nameof(package));

        int coursesAdded = 0;
        int studentsAdded = 0;
        int topicsAdded = 0;
        int questionsAdded = 0;

        // Ensure default program & user
        var existingProgram = await _context.Programs.FirstOrDefaultAsync();
        int defaultProgramId = existingProgram?.Id ?? 1;
        if (existingProgram == null)
        {
            var newProgram = new Program { Code = "GENEL", Name = "Genel Program" };
            _context.Programs.Add(newProgram);
            await _context.SaveChangesAsync();
            defaultProgramId = newProgram.Id;
        }

        var existingUser = await _context.Users.FirstOrDefaultAsync();
        int defaultUserId = existingUser?.Id ?? 1;
        if (existingUser == null)
        {
            var newUser = new User { FullName = "Admin", Email = "admin@edu.local", PasswordHash = "123", Role = UserRole.Admin, CreatedAt = DateTime.UtcNow };
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            defaultUserId = newUser.Id;
        }

        // Process Courses
        var existingCourseCodes = await _context.Courses.Select(c => c.Code).ToListAsync();
        if (package.Courses != null)
        {
            foreach (var c in package.Courses)
            {
                if (!string.IsNullOrWhiteSpace(c.Code) && !existingCourseCodes.Contains(c.Code))
                {
                    _context.Courses.Add(new Course
                    {
                        Code = c.Code,
                        Name = string.IsNullOrWhiteSpace(c.Name) ? "Ä°simsiz Ders" : c.Name,
                        ProgramId = defaultProgramId,
                        CreatedByUserId = defaultUserId
                    });
                    existingCourseCodes.Add(c.Code);
                    coursesAdded++;
                }
            }
        }
        await _context.SaveChangesAsync();

        // Reload courses to get IDs
        var allCourses = await _context.Courses.ToDictionaryAsync(c => c.Code, c => c.Id);

        // Process Students
        var existingStudentNumbers = await _context.Students.Select(s => s.StudentNumber).ToListAsync();
        if (package.Students != null)
        {
            foreach (var s in package.Students)
            {
                if (!string.IsNullOrWhiteSpace(s.Number) && !existingStudentNumbers.Contains(s.Number))
                {
                    _context.Students.Add(new Student
                    {
                        StudentNumber = s.Number,
                        FullName = string.IsNullOrWhiteSpace(s.FirstName) && string.IsNullOrWhiteSpace(s.LastName) ? "Ä°simsiz Ã–ÄŸrenci" : $"{s.FirstName} {s.LastName}".Trim(),
                        ClassName = string.IsNullOrWhiteSpace(s.ClassName) ? "BelirtilmemiÅŸ" : s.ClassName
                    });
                    existingStudentNumbers.Add(s.Number);
                    studentsAdded++;
                }
            }
        }
        await _context.SaveChangesAsync();

        // Link students to courses
        var allCourseCodesInPackage = package.Courses?.Select(c => c.Code).ToList() ?? new List<string>();
        var allStudentNumbersInPackage = package.Students?.Select(s => s.Number).ToList() ?? new List<string>();

        if (allCourseCodesInPackage.Any() && allStudentNumbersInPackage.Any())
        {
            var coursesToLink = await _context.Courses.Where(c => allCourseCodesInPackage.Contains(c.Code)).ToListAsync();
            var studentsToLink = await _context.Students.Where(s => allStudentNumbersInPackage.Contains(s.StudentNumber)).ToListAsync();
            var existingLinks = await _context.StudentCourses.ToListAsync();

            foreach(var c in coursesToLink)
            {
                foreach(var s in studentsToLink)
                {
                    if (!existingLinks.Any(sc => sc.CourseId == c.Id && sc.StudentId == s.Id))
                    {
                        _context.StudentCourses.Add(new StudentCourse { CourseId = c.Id, StudentId = s.Id });
                    }
                }
            }
            await _context.SaveChangesAsync();
        }

        // Process Topics
        var existingTopics = await _context.Topics.Include(t => t.Course).ToListAsync();
        if (package.Topics != null)
        {
            foreach (var t in package.Topics)
            {
                if (!allCourses.TryGetValue(t.CourseCode, out int courseId)) continue;

                if (!existingTopics.Any(et => et.CourseId == courseId && et.Title == t.Title))
                {
                    var newTopic = new Topic
                    {
                        CourseId = courseId,
                        WeekNumber = t.WeekNumber,
                        Title = t.Title,
                        Description = t.Description,
                    };
                    _context.Topics.Add(newTopic);
                    existingTopics.Add(newTopic);
                    topicsAdded++;
                }
            }
            await _context.SaveChangesAsync();
        }

        // Reload topics for questions mapping
        var allTopics = await _context.Topics.ToListAsync();

        // Process Questions
        if (package.Questions != null)
        {
            var existingQuestionTexts = await _context.Questions.Select(q => q.QuestionText).ToListAsync();
            foreach (var q in package.Questions)
            {
                if (!allCourses.TryGetValue(q.CourseCode, out int courseId)) continue;
                
                if (existingQuestionTexts.Contains(q.Content)) continue; // Prevent duplicates

                QuestionType qType = q.QuestionType == "OpenEnded" ? QuestionType.OpenEnded : QuestionType.MultipleChoice;

                var newQuestion = new Question
                {
                    CourseId = courseId,
                    QuestionText = q.Content,
                    Type = qType,
                    MaxPoints = q.ClassicMaxScore ?? 1.0m,
                    AnswerKey = q.ClassicAnswerKey,
                    CreatedByUserId = defaultUserId,
                    CreatedAt = DateTime.UtcNow
                };

                // Map Choices
                if (q.Choices != null && q.Choices.Any() && qType == QuestionType.MultipleChoice)
                {
                    var dict = q.Choices.ToDictionary(c => c.Key, c => c);
                    if (dict.TryGetValue("A", out var a)) { newQuestion.OptionA = a.Content; if (a.IsCorrect) newQuestion.CorrectOption = OptionLetter.A; }
                    if (dict.TryGetValue("B", out var b)) { newQuestion.OptionB = b.Content; if (b.IsCorrect) newQuestion.CorrectOption = OptionLetter.B; }
                    if (dict.TryGetValue("C", out var c)) { newQuestion.OptionC = c.Content; if (c.IsCorrect) newQuestion.CorrectOption = OptionLetter.C; }
                    if (dict.TryGetValue("D", out var d)) { newQuestion.OptionD = d.Content; if (d.IsCorrect) newQuestion.CorrectOption = OptionLetter.D; }
                    if (dict.TryGetValue("E", out var e)) { newQuestion.OptionE = e.Content; if (e.IsCorrect) newQuestion.CorrectOption = OptionLetter.E; }
                }

                _context.Questions.Add(newQuestion);
                questionsAdded++;

                // Map Topic
                if (!string.IsNullOrWhiteSpace(q.TopicTitle))
                {
                    var matchedTopic = allTopics.FirstOrDefault(t => t.CourseId == courseId && t.Title == q.TopicTitle);
                    if (matchedTopic != null)
                    {
                        _context.QuestionTopics.Add(new QuestionTopic { Question = newQuestion, TopicId = matchedTopic.Id });
                    }
                }

                // Map Rubrics
                if (q.RubricCriteria != null && q.RubricCriteria.Any() && qType == QuestionType.OpenEnded)
                {
                    foreach (var r in q.RubricCriteria)
                    {
                        _context.QuestionRubricCriteria.Add(new QuestionRubricCriterion
                        {
                            Question = newQuestion,
                            Title = r.Name,
                            MaxPoints = r.MaxScore,
                            Order = r.OrderIndex
                        });
                    }
                }

                existingQuestionTexts.Add(q.Content);
            }
            await _context.SaveChangesAsync();
        }

        return (coursesAdded, studentsAdded, topicsAdded, questionsAdded);
    }
}
