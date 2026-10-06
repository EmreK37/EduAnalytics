using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class ExamCrudService : IExamCrudService
{
    private readonly EduAnalyticsDbContext _context;
    private static readonly string[] BookletCodes = { "A", "B", "C", "D" };

    public ExamCrudService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<List<Course>> GetCoursesAsync()
    {
        return await _context.Courses
            .Include(c => c.Program)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<Course>> GetCoursesByProgramAsync(int programId)
    {
        return await _context.Courses
            .Include(c => c.Program)
            .Where(c => c.ProgramId == programId)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<Topic>> GetTopicsForCourseAsync(int courseId)
    {
        return await _context.Topics
            .Where(t => t.CourseId == courseId)
            .OrderBy(t => t.WeekNumber)
            .ToListAsync();
    }

    public async Task<List<Student>> GetStudentsAsync()
    {
        return await _context.Students
            .OrderBy(s => s.StudentNumber)
            .ToListAsync();
    }

    public async Task<int> CreateExamAsync(ExamCreateModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
            throw new ArgumentException("Sınav başlığı boş olamaz.", nameof(model));
        if (model.Questions.Count == 0)
            throw new ArgumentException("En az bir soru eklemeniz gerekiyor.", nameof(model));

        using var tx = await _context.Database.BeginTransactionAsync();

        // 1) Sınav kaydı
        var exam = new Exam
        {
            CourseId = model.CourseId,
            Title = model.Title.Trim(),
            ExamDate = model.ExamDate,
            DurationMinutes = model.DurationMinutes <= 0 ? 60 : model.DurationMinutes,
            ExamType = model.ExamType,
            BookletCount = Math.Clamp(model.BookletCount, 1, 4),
            ShuffleOptions = model.ShuffleOptions,
            CreatedByUserId = model.CreatedByUserId
        };
        _context.Exams.Add(exam);
        await _context.SaveChangesAsync();

        // 2) Sorular: soru bankasına ekle + ExamQuestion ile sınava bağla
        var examQuestions = new List<ExamQuestion>();
        int order = 1;
        foreach (var qm in model.Questions.OrderBy(q => q.QuestionNumber))
        {
            var q = new Question
            {
                CourseId = model.CourseId,
                QuestionGroupId = qm.QuestionGroupId,
                QuestionText = qm.QuestionText.Trim(),
                Type = qm.Type,
                MaxPoints = qm.MaxPoints,
                OptionA = qm.OptionA ?? string.Empty,
                OptionB = qm.OptionB ?? string.Empty,
                OptionC = qm.OptionC ?? string.Empty,
                OptionD = qm.OptionD ?? string.Empty,
                OptionE = qm.OptionE ?? string.Empty,
                CorrectOption = qm.CorrectOption,
                AnswerKey = qm.AnswerKey,
                IsActive = qm.IsActive,
                IsFavorite = qm.IsFavorite,
                CreatedByUserId = model.CreatedByUserId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Questions.Add(q);
            await _context.SaveChangesAsync();

            examQuestions.Add(new ExamQuestion
            {
                ExamId = exam.Id,
                QuestionId = q.Id,
                OrderInExam = order++,
                OverrideMaxPoints = null,
                IsCancelled = false
            });

            if (qm.TopicIds != null)
            {
                foreach (var topicId in qm.TopicIds.Distinct())
                    _context.QuestionTopics.Add(new QuestionTopic { QuestionId = q.Id, TopicId = topicId });
            }

            if (qm.LearningOutcomeIds != null)
            {
                foreach (var loId in qm.LearningOutcomeIds.Distinct())
                    _context.QuestionLearningOutcomes.Add(new QuestionLearningOutcome { QuestionId = q.Id, LearningOutcomeId = loId });
            }
        }
        _context.ExamQuestions.AddRange(examQuestions);
        await _context.SaveChangesAsync();

        // 3) Kitapçık üretimi
        var bookletCount = exam.BookletCount;
        var booklets = new List<ExamBooklet>();
        for (int i = 0; i < bookletCount; i++)
        {
            var b = new ExamBooklet { ExamId = exam.Id, BookletCode = BookletCodes[i] };
            _context.ExamBooklets.Add(b);
            booklets.Add(b);
        }
        await _context.SaveChangesAsync();

        var rng = new Random();
        foreach (var booklet in booklets)
        {
            var orderedIds = examQuestions
                .OrderBy(eq => eq.OrderInExam)
                .Select(eq => eq.QuestionId)
                .ToList();

            // İlk kitapçık (A) standart sıra; B/C/D rastgele
            if (booklet.BookletCode != "A")
                orderedIds = orderedIds.OrderBy(_ => rng.Next()).ToList();

            for (int idx = 0; idx < orderedIds.Count; idx++)
            {
                _context.ExamBookletQuestions.Add(new ExamBookletQuestion
                {
                    BookletId = booklet.Id,
                    QuestionId = orderedIds[idx],
                    OrderInBooklet = idx + 1,
                    OptionShuffleMap = (model.ShuffleOptions && booklet.BookletCode != "A")
                        ? GenerateShuffleMap(rng)
                        : null
                });
            }
        }
        await _context.SaveChangesAsync();

        // 4) Öğrenci kayıtları (varsa)
        if (model.Students != null && model.Students.Count > 0)
        {
            var incomingNumbers = model.Students.Select(s => s.StudentNumber).ToList();
            var existingStudents = await _context.Students
                .Where(s => incomingNumbers.Contains(s.StudentNumber))
                .ToListAsync();
            var existingNumbers = existingStudents.Select(s => s.StudentNumber).ToHashSet();

            var newStudents = model.Students
                .Where(ms => !existingNumbers.Contains(ms.StudentNumber))
                .Select(ms => new Student
                {
                    StudentNumber = ms.StudentNumber,
                    FullName = ms.FullName,
                    ClassName = string.IsNullOrWhiteSpace(ms.ClassName) ? "Tanımsız" : ms.ClassName
                })
                .ToList();

            if (newStudents.Count > 0)
            {
                _context.Students.AddRange(newStudents);
                await _context.SaveChangesAsync();
                existingStudents.AddRange(newStudents);
            }

            var currentEnrollments = await _context.StudentCourses
                .Where(sc => sc.CourseId == model.CourseId)
                .Select(sc => sc.StudentId)
                .ToHashSetAsync();

            var newEnrollments = existingStudents
                .Where(st => !currentEnrollments.Contains(st.Id))
                .Select(st => new StudentCourse { CourseId = model.CourseId, StudentId = st.Id })
                .ToList();

            if (newEnrollments.Count > 0)
            {
                _context.StudentCourses.AddRange(newEnrollments);
                await _context.SaveChangesAsync();
            }
        }

        await tx.CommitAsync();
        return exam.Id;
    }

    public async Task<int> CreateExamFromBankAsync(ExamFromBankCreateModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
            throw new ArgumentException("Sınav başlığı boş olamaz.", nameof(model));
        if (model.SelectedQuestions.Count == 0)
            throw new ArgumentException("En az bir soru seçilmeli.", nameof(model));

        // Seçilen sorular gerçekten bu derse ait ve aktif mi?
        var qIds = model.SelectedQuestions.Select(s => s.QuestionId).Distinct().ToList();
        var bankQuestions = await _context.Questions
            .Where(q => qIds.Contains(q.Id))
            .ToListAsync();

        if (bankQuestions.Count != qIds.Count)
            throw new InvalidOperationException("Seçilen sorulardan bazıları soru bankasında bulunamadı.");

        var wrongCourse = bankQuestions.Where(q => q.CourseId != model.CourseId).ToList();
        if (wrongCourse.Count > 0)
            throw new InvalidOperationException("Seçilen sorulardan bazıları bu derse ait değil.");

        var inactive = bankQuestions.Where(q => !q.IsActive).ToList();
        if (inactive.Count > 0)
            throw new InvalidOperationException($"Pasif soru sınava eklenemez: {inactive.Count} adet.");

        using var tx = await _context.Database.BeginTransactionAsync();

        var exam = new Exam
        {
            CourseId = model.CourseId,
            Title = model.Title.Trim(),
            ExamDate = model.ExamDate,
            DurationMinutes = model.DurationMinutes <= 0 ? 60 : model.DurationMinutes,
            ExamType = model.ExamType,
            BookletCount = Math.Clamp(model.BookletCount, 1, 4),
            ShuffleOptions = model.ShuffleOptions,
            CreatedByUserId = model.CreatedByUserId
        };
        _context.Exams.Add(exam);
        await _context.SaveChangesAsync();

        var examQuestions = model.SelectedQuestions
            .OrderBy(s => s.OrderInExam)
            .Select((s, i) => new ExamQuestion
            {
                ExamId = exam.Id,
                QuestionId = s.QuestionId,
                OrderInExam = i + 1,
                OverrideMaxPoints = s.OverrideMaxPoints,
                IsCancelled = false
            })
            .ToList();
        _context.ExamQuestions.AddRange(examQuestions);
        await _context.SaveChangesAsync();

        // Kitapçık üretimi
        var rng = new Random();
        var newBooklets = new List<ExamBooklet>();
        for (int i = 0; i < exam.BookletCount; i++)
        {
            var b = new ExamBooklet { ExamId = exam.Id, BookletCode = BookletCodes[i] };
            _context.ExamBooklets.Add(b);
            newBooklets.Add(b);
        }
        await _context.SaveChangesAsync();

        var orderedIds = examQuestions
            .OrderBy(eq => eq.OrderInExam)
            .Select(eq => eq.QuestionId)
            .ToList();

        foreach (var booklet in newBooklets)
        {
            var idsForBooklet = booklet.BookletCode == "A"
                ? orderedIds
                : orderedIds.OrderBy(_ => rng.Next()).ToList();

            for (int idx = 0; idx < idsForBooklet.Count; idx++)
            {
                _context.ExamBookletQuestions.Add(new ExamBookletQuestion
                {
                    BookletId = booklet.Id,
                    QuestionId = idsForBooklet[idx],
                    OrderInBooklet = idx + 1,
                    OptionShuffleMap = (model.ShuffleOptions && booklet.BookletCode != "A")
                        ? GenerateShuffleMap(rng)
                        : null
                });
            }
        }
        await _context.SaveChangesAsync();

        // Öğrenci kayıtları (varsa)
        if (model.Students != null && model.Students.Count > 0)
            await EnrollStudentsAsync(model.CourseId, model.Students);

        await tx.CommitAsync();
        return exam.Id;
    }

    private async Task EnrollStudentsAsync(int courseId, List<StudentCreateModel> models)
    {
        var incomingNumbers = models.Select(s => s.StudentNumber).ToList();
        var existingStudents = await _context.Students
            .Where(s => incomingNumbers.Contains(s.StudentNumber))
            .ToListAsync();
        var existingNumbers = existingStudents.Select(s => s.StudentNumber).ToHashSet();

        var newStudents = models
            .Where(ms => !existingNumbers.Contains(ms.StudentNumber))
            .Select(ms => new Student
            {
                StudentNumber = ms.StudentNumber,
                FullName = ms.FullName,
                ClassName = string.IsNullOrWhiteSpace(ms.ClassName) ? "Tanımsız" : ms.ClassName
            })
            .ToList();

        if (newStudents.Count > 0)
        {
            _context.Students.AddRange(newStudents);
            await _context.SaveChangesAsync();
            existingStudents.AddRange(newStudents);
        }

        var currentEnrollments = await _context.StudentCourses
            .Where(sc => sc.CourseId == courseId)
            .Select(sc => sc.StudentId)
            .ToHashSetAsync();

        var newEnrollments = existingStudents
            .Where(st => !currentEnrollments.Contains(st.Id))
            .Select(st => new StudentCourse { CourseId = courseId, StudentId = st.Id })
            .ToList();

        if (newEnrollments.Count > 0)
        {
            _context.StudentCourses.AddRange(newEnrollments);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetDefaultUserIdAsync()
    {
        var user = await _context.Users.FirstOrDefaultAsync();
        return user?.Id ?? throw new InvalidOperationException("Sistemde kullanıcı bulunamadı.");
    }

    public async Task<List<ExamListItemDto>> GetAllExamsAsync()
    {
        return await _context.Exams
            .Include(e => e.Course)
            .Include(e => e.ExamQuestions)
                .ThenInclude(eq => eq.Question)
            .Include(e => e.StudentAnswers)
            .OrderByDescending(e => e.ExamDate)
            .Select(e => new ExamListItemDto
            {
                Id = e.Id,
                Title = e.Title,
                CourseId = e.CourseId,
                CourseName = e.Course.Name,
                ExamDate = e.ExamDate,
                DurationMinutes = e.DurationMinutes,
                ExamType = e.ExamType,
                BookletCount = e.BookletCount,
                ShuffleOptions = e.ShuffleOptions,
                TotalQuestions = e.ExamQuestions.Count,
                TotalAnswers = e.StudentAnswers.Count,
                Questions = e.ExamQuestions
                    .OrderBy(eq => eq.OrderInExam)
                    .Select(eq => new ExamListQuestionDto
                    {
                        QuestionId = eq.QuestionId,
                        QuestionNumber = eq.OrderInExam,
                        QuestionText = eq.Question.QuestionText,
                        Type = eq.Question.Type,
                        MaxPoints = eq.OverrideMaxPoints ?? eq.Question.MaxPoints,
                        IsCancelled = eq.IsCancelled
                    })
                    .ToList()
            })
            .ToListAsync();
    }

    public async Task<ExamUpdateModel?> GetExamForEditAsync(int examId)
    {
        var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId);
        if (exam == null) return null;

        return new ExamUpdateModel
        {
            Id = exam.Id,
            Title = exam.Title,
            ExamDate = exam.ExamDate,
            DurationMinutes = exam.DurationMinutes,
            ExamType = exam.ExamType
        };
    }

    public async Task UpdateExamAsync(ExamUpdateModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
            throw new ArgumentException("Sınav başlığı boş olamaz.", nameof(model));

        var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == model.Id)
            ?? throw new InvalidOperationException($"Sınav bulunamadı: {model.Id}");

        exam.Title = model.Title.Trim();
        exam.ExamDate = model.ExamDate;
        exam.DurationMinutes = model.DurationMinutes <= 0 ? 60 : model.DurationMinutes;
        exam.ExamType = model.ExamType;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteExamAsync(int examId)
    {
        var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId)
            ?? throw new InvalidOperationException($"Sınav bulunamadı: {examId}");

        _context.Exams.Remove(exam);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Şık karıştırma haritası üretir. "A:C,B:A,C:D,D:B,E:E" formatında.
    /// </summary>
    private static string GenerateShuffleMap(Random rng)
    {
        var letters = new[] { "A", "B", "C", "D", "E" };
        var shuffled = letters.OrderBy(_ => rng.Next()).ToArray();
        return string.Join(",", letters.Select((orig, i) => $"{orig}:{shuffled[i]}"));
    }
}
