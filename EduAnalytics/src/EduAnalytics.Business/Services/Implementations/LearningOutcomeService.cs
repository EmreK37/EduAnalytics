using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using EduAnalytics.Core.Enums;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class LearningOutcomeService : ILearningOutcomeService
{
    private readonly EduAnalyticsDbContext _context;

    public LearningOutcomeService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<List<LearningOutcomeDto>> GetByCourseAsync(int courseId)
    {
        return await _context.LearningOutcomes
            .Where(lo => lo.CourseId == courseId)
            .Select(lo => new LearningOutcomeDto
            {
                Id = lo.Id,
                CourseId = lo.CourseId,
                Code = lo.Code,
                Name = lo.Name,
                Description = lo.Description,
                TopicCount = lo.TopicLearningOutcomes.Count,
                QuestionCount = lo.QuestionLearningOutcomes.Count,
                ProgramOutcomeCodes = lo.ProgramOutcomeMappings
                    .Select(m => m.ProgramOutcome.Code)
                    .ToList()
            })
            .OrderBy(lo => lo.Code)
            .ToListAsync();
    }

    public async Task<List<LearningOutcomeDto>> GetAvailableForExamAsync(int courseId, ExamType examType, int? cutoffWeek = null)
    {
        // Final ve MakeUp: tüm ÖÇ'ler kullanılabilir
        if (examType == ExamType.Final || examType == ExamType.MakeUp)
            return await GetByCourseAsync(courseId);

        // Vize / Quiz: sadece cutoffWeek'e kadar olan haftalardaki konulara bağlı ÖÇ'ler
        // cutoffWeek verilmezse Vize için 7. hafta varsayılan, Quiz için 4. hafta varsayılan
        var cutoff = cutoffWeek ?? (examType == ExamType.Midterm ? 7 : 4);

        var availableLoIds = await _context.LearningOutcomes
            .Where(lo => lo.CourseId == courseId)
            .Where(lo => lo.TopicLearningOutcomes
                .Any(tl => tl.Topic.WeekNumber <= cutoff))
            .Select(lo => lo.Id)
            .ToListAsync();

        return await _context.LearningOutcomes
            .Where(lo => availableLoIds.Contains(lo.Id))
            .Select(lo => new LearningOutcomeDto
            {
                Id = lo.Id,
                CourseId = lo.CourseId,
                Code = lo.Code,
                Name = lo.Name,
                Description = lo.Description,
                TopicCount = lo.TopicLearningOutcomes.Count(tl => tl.Topic.WeekNumber <= cutoff),
                QuestionCount = lo.QuestionLearningOutcomes.Count,
                ProgramOutcomeCodes = lo.ProgramOutcomeMappings
                    .Select(m => m.ProgramOutcome.Code)
                    .ToList()
            })
            .OrderBy(lo => lo.Code)
            .ToListAsync();
    }

    public async Task<int> CreateAsync(LearningOutcomeCreateModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Code))
            throw new ArgumentException("ÖÇ kodu zorunludur.", nameof(model));
        if (string.IsNullOrWhiteSpace(model.Name))
            throw new ArgumentException("ÖÇ adı zorunludur.", nameof(model));

        var lo = new LearningOutcome
        {
            CourseId = model.CourseId,
            Code = model.Code.Trim(),
            Name = model.Name.Trim(),
            Description = model.Description?.Trim()
        };
        _context.LearningOutcomes.Add(lo);
        await _context.SaveChangesAsync();

        foreach (var tid in model.TopicIds.Distinct())
            _context.TopicLearningOutcomes.Add(new TopicLearningOutcome
            {
                LearningOutcomeId = lo.Id,
                TopicId = tid
            });

        foreach (var pm in model.ProgramOutcomeMappings)
            _context.ProgramOutcomeMappings.Add(new ProgramOutcomeMapping
            {
                LearningOutcomeId = lo.Id,
                ProgramOutcomeId = pm.ProgramOutcomeId,
                ContributionLevel = Math.Clamp(pm.ContributionLevel, 1, 5)
            });

        await _context.SaveChangesAsync();
        return lo.Id;
    }

    public async Task UpdateAsync(int learningOutcomeId, LearningOutcomeCreateModel model)
    {
        var lo = await _context.LearningOutcomes
            .Include(l => l.TopicLearningOutcomes)
            .Include(l => l.ProgramOutcomeMappings)
            .FirstOrDefaultAsync(l => l.Id == learningOutcomeId)
            ?? throw new InvalidOperationException($"ÖÇ bulunamadı: {learningOutcomeId}");

        lo.Code = model.Code.Trim();
        lo.Name = model.Name.Trim();
        lo.Description = model.Description?.Trim();

        _context.TopicLearningOutcomes.RemoveRange(lo.TopicLearningOutcomes);
        _context.ProgramOutcomeMappings.RemoveRange(lo.ProgramOutcomeMappings);
        await _context.SaveChangesAsync();

        foreach (var tid in model.TopicIds.Distinct())
            _context.TopicLearningOutcomes.Add(new TopicLearningOutcome
            {
                LearningOutcomeId = lo.Id,
                TopicId = tid
            });

        foreach (var pm in model.ProgramOutcomeMappings)
            _context.ProgramOutcomeMappings.Add(new ProgramOutcomeMapping
            {
                LearningOutcomeId = lo.Id,
                ProgramOutcomeId = pm.ProgramOutcomeId,
                ContributionLevel = Math.Clamp(pm.ContributionLevel, 1, 5)
            });

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int learningOutcomeId)
    {
        var inUse = await _context.QuestionLearningOutcomes.AnyAsync(ql => ql.LearningOutcomeId == learningOutcomeId);
        if (inUse)
            throw new InvalidOperationException("Bu ÖÇ sorulara bağlı. Önce ilişkileri kaldırın.");

        var lo = await _context.LearningOutcomes.FindAsync(learningOutcomeId)
            ?? throw new InvalidOperationException($"ÖÇ bulunamadı: {learningOutcomeId}");

        _context.LearningOutcomes.Remove(lo);
        await _context.SaveChangesAsync();
    }

    public async Task LinkToTopicAsync(int learningOutcomeId, int topicId)
    {
        var exists = await _context.TopicLearningOutcomes
            .AnyAsync(tl => tl.LearningOutcomeId == learningOutcomeId && tl.TopicId == topicId);
        if (exists) return;

        _context.TopicLearningOutcomes.Add(new TopicLearningOutcome
        {
            LearningOutcomeId = learningOutcomeId,
            TopicId = topicId
        });
        await _context.SaveChangesAsync();
    }

    public async Task UnlinkFromTopicAsync(int learningOutcomeId, int topicId)
    {
        var link = await _context.TopicLearningOutcomes
            .FirstOrDefaultAsync(tl => tl.LearningOutcomeId == learningOutcomeId && tl.TopicId == topicId);

        if (link != null)
        {
            _context.TopicLearningOutcomes.Remove(link);
            await _context.SaveChangesAsync();
        }
    }
}
