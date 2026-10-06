using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Enums;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class LearningOutcomePerformanceService : ILearningOutcomePerformanceService
{
    private readonly EduAnalyticsDbContext _context;

    public LearningOutcomePerformanceService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<List<LearningOutcomePerformanceDto>> AnalyzeExamAsync(int examId)
    {
        // Sadece test sorularını analiz et (klasik için manuel puan, çıktı bağlamı farklı)
        var examQuestions = await _context.ExamQuestions
            .Where(eq => eq.ExamId == examId
                      && !eq.IsCancelled
                      && eq.Question.Type == QuestionType.MultipleChoice)
            .Include(eq => eq.Question)
                .ThenInclude(q => q.QuestionLearningOutcomes)
                    .ThenInclude(qlo => qlo.LearningOutcome)
            .ToListAsync();

        var qIds = examQuestions.Select(eq => eq.QuestionId).ToList();
        var answers = await _context.StudentAnswers
            .Where(sa => sa.ExamId == examId && qIds.Contains(sa.QuestionId))
            .ToListAsync();

        var answerLookup = answers.ToLookup(a => a.QuestionId);

        var allOutcomes = examQuestions
            .SelectMany(eq => eq.Question.QuestionLearningOutcomes.Select(qlo => qlo.LearningOutcome))
            .DistinctBy(lo => lo.Id)
            .ToList();

        var result = new List<LearningOutcomePerformanceDto>();

        foreach (var outcome in allOutcomes)
        {
            var relatedEqs = examQuestions
                .Where(eq => eq.Question.QuestionLearningOutcomes.Any(qlo => qlo.LearningOutcomeId == outcome.Id))
                .ToList();

            var relatedAnswers = relatedEqs
                .SelectMany(eq => answerLookup[eq.QuestionId])
                .ToList();

            if (relatedAnswers.Count == 0) continue;

            int correctCount = relatedAnswers.Count(a => a.IsCorrect);
            double successRate = (double)correctCount / relatedAnswers.Count * 100;

            result.Add(new LearningOutcomePerformanceDto
            {
                LearningOutcomeId = outcome.Id,
                OutcomeName = $"{outcome.Code} — {outcome.Name}",
                Description = outcome.Description,
                RelatedQuestionCount = relatedEqs.Count,
                TotalAnswers = relatedAnswers.Count,
                CorrectAnswers = correctCount,
                SuccessRate = successRate
            });
        }

        return result.OrderByDescending(r => r.SuccessRate).ToList();
    }
}
