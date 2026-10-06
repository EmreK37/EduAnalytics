using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using EduAnalytics.Core.Enums;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class AnswerEntryService : IAnswerEntryService
{
    private readonly EduAnalyticsDbContext _context;
    private readonly IExamBookletService _bookletService;

    public AnswerEntryService(EduAnalyticsDbContext context, IExamBookletService bookletService)
    {
        _context = context;
        _bookletService = bookletService;
    }

    public async Task<AnswerEntryModel> LoadAsync(int examId)
    {
        var exam = await _context.Exams
            .Include(e => e.Course)
            .Include(e => e.ExamQuestions.OrderBy(eq => eq.OrderInExam))
                .ThenInclude(eq => eq.Question)
            .FirstOrDefaultAsync(e => e.Id == examId)
            ?? throw new InvalidOperationException($"Sınav bulunamadı: {examId}");

        var students = await _context.StudentCourses
            .Where(sc => sc.CourseId == exam.CourseId)
            .Select(sc => sc.Student)
            .OrderBy(s => s.StudentNumber)
            .ToListAsync();

        var booklets = await _context.ExamBooklets
            .Where(b => b.ExamId == examId)
            .OrderBy(b => b.BookletCode)
            .ToListAsync();

        var bookletMap = booklets.ToDictionary(b => b.Id);

        var model = new AnswerEntryModel
        {
            ExamId = exam.Id,
            ExamTitle = exam.Title,
            CourseName = exam.Course.Name,
            BookletCount = exam.BookletCount,
            AvailableBooklets = booklets.Select(b => new BookletInfo
            {
                BookletId = b.Id,
                BookletCode = b.BookletCode
            }).ToList(),
            Questions = exam.ExamQuestions
                .OrderBy(eq => eq.OrderInExam)
                .Select(eq => new AnswerEntryQuestion
                {
                    QuestionId = eq.QuestionId,
                    QuestionNumber = eq.OrderInExam,
                    Type = eq.Question.Type,
                    MaxPoints = eq.OverrideMaxPoints ?? eq.Question.MaxPoints,
                    CorrectOption = eq.Question.CorrectOption,
                    IsCancelled = eq.IsCancelled,
                    QuestionTextPreview = eq.Question.QuestionText.Length > 60
                        ? eq.Question.QuestionText[..60] + "…"
                        : eq.Question.QuestionText
                })
                .ToList()
        };

        var qIds = exam.ExamQuestions.Select(eq => eq.QuestionId).ToList();
        var answers = await _context.StudentAnswers
            .Where(sa => sa.ExamId == examId && qIds.Contains(sa.QuestionId))
            .ToListAsync();

        var answerLookup = answers.ToLookup(a => a.StudentId);

        foreach (var s in students)
        {
            var firstAnswer = answerLookup[s.Id].FirstOrDefault();
            var row = new AnswerEntryStudent
            {
                StudentId = s.Id,
                StudentNumber = s.StudentNumber,
                FullName = s.FullName,
                BookletId = firstAnswer?.BookletId,
                BookletCode = firstAnswer?.BookletId.HasValue == true && bookletMap.TryGetValue(firstAnswer.BookletId.Value, out var b)
                    ? b.BookletCode
                    : null
            };

            foreach (var a in answerLookup[s.Id])
            {
                row.Answers[a.QuestionId] = new AnswerEntryCell
                {
                    SelectedOption = a.SelectedOption,
                    Score = a.Score
                };
            }

            model.Students.Add(row);
        }

        return model;
    }

    public async Task SaveAsync(int examId, List<StudentAnswerUpdate> updates)
    {
        var examQuestions = await _context.ExamQuestions
            .Where(eq => eq.ExamId == examId)
            .Include(eq => eq.Question)
            .ToDictionaryAsync(eq => eq.QuestionId);

        var qIds = examQuestions.Keys.ToList();
        var studentIds = updates.Select(u => u.StudentId).Distinct().ToList();

        var existing = await _context.StudentAnswers
            .Where(sa => sa.ExamId == examId
                      && qIds.Contains(sa.QuestionId)
                      && studentIds.Contains(sa.StudentId))
            .ToListAsync();

        var existingLookup = existing.ToDictionary(sa => (sa.QuestionId, sa.StudentId));

        // Kitapçık-bazlı şık permütasyonu için lookup
        var bookletQuestions = await _context.ExamBookletQuestions
            .Include(bq => bq.Booklet)
            .Where(bq => bq.Booklet.ExamId == examId)
            .ToListAsync();
        var shuffleMapLookup = bookletQuestions
            .ToDictionary(bq => (bq.BookletId, bq.QuestionId), bq => bq.OptionShuffleMap);

        var defaultBookletId = await _context.ExamBooklets
            .Where(b => b.ExamId == examId && b.BookletCode == "A")
            .Select(b => (int?)b.Id)
            .FirstOrDefaultAsync();

        foreach (var u in updates)
        {
            if (!examQuestions.TryGetValue(u.QuestionId, out var eq))
                continue;

            var max = eq.OverrideMaxPoints ?? eq.Question.MaxPoints;
            var bookletId = u.BookletId ?? defaultBookletId;

            // Kitapçıkta şıklar karıştırıldıysa, öğrencinin işaretlediği şıkkı orijinal şıkka çevir
            var decodedOption = u.SelectedOption;
            if (eq.Question.Type == QuestionType.MultipleChoice
                && bookletId.HasValue
                && shuffleMapLookup.TryGetValue((bookletId.Value, u.QuestionId), out var shuffleMap)
                && !string.IsNullOrEmpty(shuffleMap))
            {
                decodedOption = _bookletService.DecodeStudentChoice(shuffleMap, u.SelectedOption);
            }

            var decodedUpdate = new StudentAnswerUpdate
            {
                ExamId = examId,
                QuestionId = u.QuestionId,
                StudentId = u.StudentId,
                BookletId = bookletId,
                SelectedOption = decodedOption,
                Score = u.Score
            };

            var isCorrect = CalculateIsCorrect(eq.Question, decodedUpdate, max);
            var key = (u.QuestionId, u.StudentId);

            if (existingLookup.TryGetValue(key, out var existingAnswer))
            {
                existingAnswer.SelectedOption = decodedOption;
                existingAnswer.Score = u.Score;
                existingAnswer.IsCorrect = isCorrect;
                existingAnswer.BookletId = bookletId;
            }
            else
            {
                if (decodedOption == OptionLetter.Empty && u.Score == null)
                    continue;

                _context.StudentAnswers.Add(new StudentAnswer
                {
                    ExamId = examId,
                    QuestionId = u.QuestionId,
                    StudentId = u.StudentId,
                    BookletId = bookletId,
                    SelectedOption = decodedOption,
                    Score = u.Score,
                    IsCorrect = isCorrect
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    private static bool CalculateIsCorrect(Question q, StudentAnswerUpdate u, decimal max)
    {
        if (q.Type == QuestionType.MultipleChoice)
            return u.SelectedOption == q.CorrectOption && u.SelectedOption != OptionLetter.Empty;

        return u.Score.HasValue && u.Score.Value >= max / 2m;
    }
}
