using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using EduAnalytics.Core.Enums;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class RubricService : IRubricService
{
    private readonly EduAnalyticsDbContext _context;

    public RubricService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<List<RubricCriterionDto>> GetCriteriaAsync(int questionId)
    {
        return await _context.QuestionRubricCriteria
            .Where(c => c.QuestionId == questionId)
            .OrderBy(c => c.Order)
            .Select(c => new RubricCriterionDto
            {
                Id = c.Id,
                QuestionId = c.QuestionId,
                Title = c.Title,
                MaxPoints = c.MaxPoints,
                Order = c.Order,
                Description = c.Description
            })
            .ToListAsync();
    }

    public async Task SetCriteriaAsync(int questionId, List<RubricCriterionCreateModel> criteria)
    {
        var existing = await _context.QuestionRubricCriteria
            .Where(c => c.QuestionId == questionId)
            .ToListAsync();

        // Mevcut kriterler bir öğrenci tarafından puanlanmışsa silmeyi engelle
        var hasScores = await _context.StudentAnswerCriteria
            .AnyAsync(s => existing.Select(e => e.Id).Contains(s.CriterionId));

        if (hasScores && existing.Count > 0)
            throw new InvalidOperationException(
                "Bu sorunun kriterleri en az bir öğrenci tarafından puanlanmış. Sıfırlama için önce puan kayıtlarını silmek gerekir.");

        _context.QuestionRubricCriteria.RemoveRange(existing);
        await _context.SaveChangesAsync();

        int order = 1;
        foreach (var c in criteria.OrderBy(x => x.Order))
        {
            _context.QuestionRubricCriteria.Add(new QuestionRubricCriterion
            {
                QuestionId = questionId,
                Title = c.Title.Trim(),
                MaxPoints = c.MaxPoints,
                Order = c.Order > 0 ? c.Order : order++,
                Description = c.Description?.Trim()
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task<StudentRubricGradeDto> GetStudentGradeAsync(int examId, int questionId, int studentId)
    {
        var criteria = await _context.QuestionRubricCriteria
            .Where(c => c.QuestionId == questionId)
            .OrderBy(c => c.Order)
            .ToListAsync();

        var studentAnswer = await _context.StudentAnswers
            .Include(sa => sa.CriterionScores)
            .FirstOrDefaultAsync(sa => sa.ExamId == examId
                                    && sa.QuestionId == questionId
                                    && sa.StudentId == studentId);

        var dto = new StudentRubricGradeDto
        {
            StudentAnswerId = studentAnswer?.Id ?? 0,
            StudentId = studentId,
            QuestionId = questionId
        };

        foreach (var c in criteria)
        {
            var existingScore = studentAnswer?.CriterionScores.FirstOrDefault(s => s.CriterionId == c.Id);
            dto.CriterionScores.Add(new CriterionScoreDto
            {
                CriterionId = c.Id,
                CriterionTitle = c.Title,
                MaxPoints = c.MaxPoints,
                Score = existingScore?.Score ?? 0m,
                Comment = existingScore?.Comment
            });
        }

        return dto;
    }

    public async Task SaveStudentGradeAsync(int examId, int questionId, int studentId, List<CriterionScoreUpdate> updates)
    {
        var question = await _context.Questions.FindAsync(questionId)
            ?? throw new InvalidOperationException($"Soru bulunamadı: {questionId}");

        var studentAnswer = await _context.StudentAnswers
            .Include(sa => sa.CriterionScores)
            .FirstOrDefaultAsync(sa => sa.ExamId == examId
                                    && sa.QuestionId == questionId
                                    && sa.StudentId == studentId);

        // Toplam puan ve doğruluk hesapla
        var totalScore = updates.Sum(u => u.Score);

        if (studentAnswer == null)
        {
            studentAnswer = new StudentAnswer
            {
                ExamId = examId,
                QuestionId = questionId,
                StudentId = studentId,
                SelectedOption = OptionLetter.Empty,
                Score = totalScore,
                IsCorrect = totalScore >= question.MaxPoints / 2m
            };
            _context.StudentAnswers.Add(studentAnswer);
            await _context.SaveChangesAsync();
        }
        else
        {
            studentAnswer.Score = totalScore;
            studentAnswer.IsCorrect = totalScore >= question.MaxPoints / 2m;
        }

        // Eski kriter puanlarını sil
        _context.StudentAnswerCriteria.RemoveRange(studentAnswer.CriterionScores);
        await _context.SaveChangesAsync();

        // Yeni kriter puanlarını ekle
        var validCriteriaIds = await _context.QuestionRubricCriteria
            .Where(c => c.QuestionId == questionId)
            .Select(c => c.Id)
            .ToHashSetAsync();

        foreach (var u in updates)
        {
            if (!validCriteriaIds.Contains(u.CriterionId)) continue;

            _context.StudentAnswerCriteria.Add(new StudentAnswerCriterion
            {
                StudentAnswerId = studentAnswer.Id,
                CriterionId = u.CriterionId,
                Score = u.Score,
                Comment = u.Comment
            });
        }

        await _context.SaveChangesAsync();
    }
}
