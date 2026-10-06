using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using EduAnalytics.Core.Enums;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class ExamAnalysisService : IExamAnalysisService
{
    private readonly EduAnalyticsDbContext _context;

    public ExamAnalysisService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Bir öğrencinin tek bir soru için aldığı puanı hesaplar.
    /// Test: doğruysa MaxPoints, yanlışsa 0.
    /// Klasik: öğretmen tarafından girilen Score, yoksa 0.
    /// </summary>
    internal static decimal ComputeScore(Question q, StudentAnswer a, decimal? overrideMax = null)
    {
        var max = overrideMax ?? q.MaxPoints;
        return q.Type == QuestionType.MultipleChoice
            ? (a.IsCorrect ? max : 0m)
            : (a.Score ?? 0m);
    }

    public async Task<ExamSummaryDto?> GetSummaryAsync(int examId)
    {
        var exam = await _context.Exams
            .Include(e => e.Course)
            .Include(e => e.ExamQuestions.Where(eq => !eq.IsCancelled))
                .ThenInclude(eq => eq.Question)
            .FirstOrDefaultAsync(e => e.Id == examId);

        if (exam == null) return null;

        var examQuestions = exam.ExamQuestions.Where(eq => !eq.IsCancelled).ToList();
        var questions = examQuestions.Select(eq => eq.Question).ToList();
        var qIds = questions.Select(q => q.Id).ToList();

        var answers = await _context.StudentAnswers
            .Where(sa => sa.ExamId == examId && qIds.Contains(sa.QuestionId))
            .ToListAsync();

        var mcCount = questions.Count(q => q.Type == QuestionType.MultipleChoice);
        var oeCount = questions.Count(q => q.Type == QuestionType.OpenEnded);
        var maxPossible = examQuestions.Sum(eq => eq.OverrideMaxPoints ?? eq.Question.MaxPoints);

        var scoreLookup = examQuestions.ToDictionary(eq => eq.QuestionId, eq => eq);

        var studentScores = answers
            .GroupBy(a => a.StudentId)
            .Select(g => g.Sum(a =>
            {
                if (!scoreLookup.TryGetValue(a.QuestionId, out var eq))
                    return 0m;
                return ComputeScore(eq.Question, a, eq.OverrideMaxPoints);
            }))
            .ToList();

        if (studentScores.Count == 0)
        {
            return new ExamSummaryDto
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                ExamDate = exam.ExamDate,
                CourseName = exam.Course.Name,
                TotalQuestions = questions.Count,
                MultipleChoiceCount = mcCount,
                OpenEndedCount = oeCount,
                MaxPossibleScore = maxPossible,
                TotalStudents = 0
            };
        }

        var avgScore = studentScores.Average();

        return new ExamSummaryDto
        {
            ExamId = exam.Id,
            ExamTitle = exam.Title,
            ExamDate = exam.ExamDate,
            CourseName = exam.Course.Name,
            TotalStudents = studentScores.Count,
            TotalQuestions = questions.Count,
            MultipleChoiceCount = mcCount,
            OpenEndedCount = oeCount,
            MaxPossibleScore = maxPossible,
            AverageScore = Math.Round(avgScore, 2),
            AverageSuccessRate = maxPossible > 0 ? Math.Round((double)(avgScore / maxPossible) * 100, 1) : 0,
            HighestScore = studentScores.Max(),
            LowestScore = studentScores.Min()
        };
    }

    public async Task<List<ExamSummaryDto>> GetAllExamSummariesAsync()
    {
        var examIds = await _context.Exams.Select(e => e.Id).ToListAsync();
        var results = new List<ExamSummaryDto>();
        foreach (var id in examIds)
        {
            var summary = await GetSummaryAsync(id);
            if (summary != null) results.Add(summary);
        }
        return results;
    }
}
