using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class ExamCancellationService : IExamCancellationService
{
    private readonly EduAnalyticsDbContext _context;

    public ExamCancellationService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task CancelQuestionAsync(int examId, int questionId, string? reason = null)
    {
        var eq = await _context.ExamQuestions
            .FirstOrDefaultAsync(e => e.ExamId == examId && e.QuestionId == questionId)
            ?? throw new InvalidOperationException($"Bu sınavda böyle bir soru yok (ExamId={examId}, QuestionId={questionId}).");

        eq.IsCancelled = true;
        eq.CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        await _context.SaveChangesAsync();
    }

    public async Task RestoreQuestionAsync(int examId, int questionId)
    {
        var eq = await _context.ExamQuestions
            .FirstOrDefaultAsync(e => e.ExamId == examId && e.QuestionId == questionId)
            ?? throw new InvalidOperationException($"Bu sınavda böyle bir soru yok (ExamId={examId}, QuestionId={questionId}).");

        eq.IsCancelled = false;
        eq.CancellationReason = null;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsCancelledAsync(int examId, int questionId)
    {
        return await _context.ExamQuestions
            .AnyAsync(eq => eq.ExamId == examId && eq.QuestionId == questionId && eq.IsCancelled);
    }
}
