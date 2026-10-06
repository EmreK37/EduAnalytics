using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class TopicPerformanceService : ITopicPerformanceService
{
    private readonly EduAnalyticsDbContext _context;

    public TopicPerformanceService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<List<TopicPerformanceDto>> AnalyzeExamAsync(int examId)
    {
        var examQuestions = await _context.ExamQuestions
            .Where(eq => eq.ExamId == examId && !eq.IsCancelled)
            .Include(eq => eq.Question)
                .ThenInclude(q => q.QuestionTopics)
                    .ThenInclude(qt => qt.Topic)
                        .ThenInclude(t => t.TopicLearningOutcomes)
                            .ThenInclude(tl => tl.LearningOutcome)
            .ToListAsync();

        var qIds = examQuestions.Select(eq => eq.QuestionId).ToList();
        var answers = await _context.StudentAnswers
            .Where(sa => sa.ExamId == examId && qIds.Contains(sa.QuestionId))
            .ToListAsync();

        var answerLookup = answers.ToLookup(a => a.QuestionId);

        // Konu → o sınavda bu konuya bağlı sorular
        var topicMap = examQuestions
            .SelectMany(eq => eq.Question.QuestionTopics.Select(qt => new
            {
                qt.Topic,
                ExamQuestion = eq
            }))
            .GroupBy(x => x.Topic.Id)
            .ToList();

        var results = new List<TopicPerformanceDto>();

        foreach (var group in topicMap)
        {
            var topic = group.First().Topic;
            var topicEqs = group.Select(g => g.ExamQuestion).Distinct().ToList();

            decimal totalEarned = 0;
            decimal totalMax = 0;
            int answerCount = 0;
            int correctCount = 0;

            foreach (var eq in topicEqs)
            {
                var max = eq.OverrideMaxPoints ?? eq.Question.MaxPoints;
                foreach (var a in answerLookup[eq.QuestionId])
                {
                    totalEarned += ExamAnalysisService.ComputeScore(eq.Question, a, eq.OverrideMaxPoints);
                    totalMax += max;
                    answerCount++;
                    if (a.IsCorrect) correctCount++;
                }
            }

            // Konunun bağlı olduğu ÖÇ'leri özet metin olarak topla
            var loSummary = topic.TopicLearningOutcomes.Count > 0
                ? string.Join(", ", topic.TopicLearningOutcomes.Select(tl => tl.LearningOutcome.Code))
                : null;

            results.Add(new TopicPerformanceDto
            {
                TopicId = topic.Id,
                WeekNumber = topic.WeekNumber,
                TopicTitle = topic.Title,
                LearningOutcome = loSummary,
                RelatedQuestionCount = topicEqs.Count,
                TotalAnswers = answerCount,
                CorrectAnswers = correctCount,
                SuccessRate = totalMax > 0 ? Math.Round((double)(totalEarned / totalMax) * 100, 1) : 0
            });
        }

        return results.OrderBy(t => t.WeekNumber).ToList();
    }

    public async Task<List<TopicPerformanceDto>> GetWeakTopicsAsync(int examId, double threshold = 60.0)
    {
        var all = await AnalyzeExamAsync(examId);
        return all.Where(t => t.SuccessRate < threshold)
                  .OrderBy(t => t.SuccessRate)
                  .ToList();
    }
}
