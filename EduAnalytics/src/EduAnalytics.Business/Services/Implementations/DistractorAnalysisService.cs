using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Enums;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class DistractorAnalysisService : IDistractorAnalysisService
{
    private readonly EduAnalyticsDbContext _context;

    public DistractorAnalysisService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<List<QuestionAnalysisDto>> AnalyzeExamAsync(int examId)
    {
        var examQuestions = await _context.ExamQuestions
            .Where(eq => eq.ExamId == examId && !eq.IsCancelled)
            .Include(eq => eq.Question)
                .ThenInclude(q => q.QuestionTopics)
                    .ThenInclude(qt => qt.Topic)
            .OrderBy(eq => eq.OrderInExam)
            .ToListAsync();

        var qIds = examQuestions.Select(eq => eq.QuestionId).ToList();
        var allAnswers = await _context.StudentAnswers
            .Where(sa => sa.ExamId == examId && qIds.Contains(sa.QuestionId))
            .ToListAsync();

        var answerLookup = allAnswers.ToLookup(a => a.QuestionId);
        var results = new List<QuestionAnalysisDto>();

        foreach (var eq in examQuestions)
        {
            var q = eq.Question;
            var max = eq.OverrideMaxPoints ?? q.MaxPoints;
            var answers = answerLookup[q.Id].ToList();

            var total = answers.Count;
            var correct = answers.Count(a => a.IsCorrect);
            var empty = answers.Count(a => a.SelectedOption == OptionLetter.Empty && a.Score == null);
            var wrong = total - correct - empty;

            var dto = new QuestionAnalysisDto
            {
                QuestionId = q.Id,
                QuestionNumber = eq.OrderInExam,
                QuestionText = q.QuestionText,
                QuestionType = q.Type.ToString(),
                MaxPoints = max,
                CorrectOption = q.Type == QuestionType.MultipleChoice ? q.CorrectOption.ToString() : "—",
                TotalAnswers = total,
                CorrectCount = correct,
                WrongCount = wrong,
                EmptyCount = empty,
                SuccessRate = total > 0 ? (double)correct / total * 100 : 0,
                LinkedTopicTitles = q.QuestionTopics.Select(qt => qt.Topic.Title).ToList()
            };

            if (q.Type == QuestionType.MultipleChoice)
            {
                dto.OptionACount = answers.Count(a => a.SelectedOption == OptionLetter.A);
                dto.OptionBCount = answers.Count(a => a.SelectedOption == OptionLetter.B);
                dto.OptionCCount = answers.Count(a => a.SelectedOption == OptionLetter.C);
                dto.OptionDCount = answers.Count(a => a.SelectedOption == OptionLetter.D);
                dto.OptionECount = answers.Count(a => a.SelectedOption == OptionLetter.E);
                dto.AverageScore = total > 0 ? (decimal)correct / total * max : 0;

                var wrongAnswers = answers
                    .Where(a => !a.IsCorrect && a.SelectedOption != OptionLetter.Empty)
                    .ToList();

                if (wrongAnswers.Count > 0)
                {
                    var distractor = wrongAnswers
                        .GroupBy(a => a.SelectedOption)
                        .Select(g => new { Option = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .First();

                    dto.StrongestDistractorOption = distractor.Option.ToString();
                    dto.StrongestDistractorCount = distractor.Count;
                    dto.StrongestDistractorRate = Math.Round((double)distractor.Count / wrongAnswers.Count * 100, 1);
                }
            }
            else
            {
                var scored = answers.Where(a => a.Score.HasValue).Select(a => a.Score!.Value).ToList();
                dto.AverageScore = scored.Count > 0 ? Math.Round(scored.Average(), 2) : 0;
                dto.SuccessRate = scored.Count > 0
                    ? Math.Round((double)(scored.Average() / max) * 100, 1)
                    : 0;
                dto.StrongestDistractorOption = null;
                dto.StrongestDistractorRate = 0;
            }

            results.Add(dto);
        }

        return results;
    }

    public async Task<List<QuestionAnalysisDto>> GetStrongDistractorsAsync(int examId, double minDistractorRate = 50.0)
    {
        var all = await AnalyzeExamAsync(examId);
        return all.Where(q => q.QuestionType == "MultipleChoice"
                           && q.StrongestDistractorRate >= minDistractorRate)
                  .OrderByDescending(q => q.StrongestDistractorRate)
                  .ToList();
    }
}
