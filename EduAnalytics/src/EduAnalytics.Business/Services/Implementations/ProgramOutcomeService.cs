using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using EduAnalytics.Core.Enums;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class ProgramOutcomeService : IProgramOutcomeService
{
    private readonly EduAnalyticsDbContext _context;

    public ProgramOutcomeService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<List<Program>> GetProgramsAsync()
    {
        return await _context.Programs.OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<List<ProgramOutcomeDto>> GetByProgramAsync(int programId)
    {
        return await _context.ProgramOutcomes
            .Include(po => po.Program)
            .Include(po => po.Mappings)
            .Where(po => po.ProgramId == programId)
            .Select(po => new ProgramOutcomeDto
            {
                Id = po.Id,
                ProgramId = po.ProgramId,
                ProgramName = po.Program.Name,
                Code = po.Code,
                Description = po.Description,
                LearningOutcomeCount = po.Mappings.Count
            })
            .OrderBy(po => po.Code)
            .ToListAsync();
    }

    public async Task<int> CreateAsync(ProgramOutcomeCreateModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Code))
            throw new ArgumentException("PÇ kodu zorunludur.", nameof(model));
        if (string.IsNullOrWhiteSpace(model.Description))
            throw new ArgumentException("PÇ açıklaması zorunludur.", nameof(model));

        var po = new ProgramOutcome
        {
            ProgramId = model.ProgramId,
            Code = model.Code.Trim(),
            Description = model.Description.Trim()
        };
        _context.ProgramOutcomes.Add(po);
        await _context.SaveChangesAsync();
        return po.Id;
    }

    public async Task UpdateAsync(int programOutcomeId, ProgramOutcomeCreateModel model)
    {
        var po = await _context.ProgramOutcomes.FindAsync(programOutcomeId)
            ?? throw new InvalidOperationException($"PÇ bulunamadı: {programOutcomeId}");

        po.Code = model.Code.Trim();
        po.Description = model.Description.Trim();
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int programOutcomeId)
    {
        var po = await _context.ProgramOutcomes
            .Include(p => p.Mappings)
            .FirstOrDefaultAsync(p => p.Id == programOutcomeId)
            ?? throw new InvalidOperationException($"PÇ bulunamadı: {programOutcomeId}");

        _context.ProgramOutcomeMappings.RemoveRange(po.Mappings);
        _context.ProgramOutcomes.Remove(po);
        await _context.SaveChangesAsync();
    }

    public async Task<List<ProgramOutcomeReportDto>> GenerateReportAsync(int programId)
    {
        // Bu programa ait tüm PÇ'leri ve onlara bağlı ÖÇ'leri çek
        var programOutcomes = await _context.ProgramOutcomes
            .Include(po => po.Mappings)
                .ThenInclude(m => m.LearningOutcome)
                    .ThenInclude(lo => lo.Course)
            .Include(po => po.Mappings)
                .ThenInclude(m => m.LearningOutcome)
                    .ThenInclude(lo => lo.QuestionLearningOutcomes)
                        .ThenInclude(ql => ql.Question)
                            .ThenInclude(q => q.ExamQuestions)
            .Include(po => po.Mappings)
                .ThenInclude(m => m.LearningOutcome)
                    .ThenInclude(lo => lo.QuestionLearningOutcomes)
                        .ThenInclude(ql => ql.Question)
                            .ThenInclude(q => q.StudentAnswers)
            .Where(po => po.ProgramId == programId)
            .ToListAsync();

        var reports = new List<ProgramOutcomeReportDto>();

        foreach (var po in programOutcomes)
        {
            var report = new ProgramOutcomeReportDto
            {
                ProgramOutcomeId = po.Id,
                ProgramOutcomeCode = po.Code,
                ProgramOutcomeDescription = po.Description
            };

            int totalCorrect = 0;
            int totalAnswered = 0;
            int totalQuestions = 0;

            foreach (var mapping in po.Mappings)
            {
                var lo = mapping.LearningOutcome;

                // Bu ÖÇ'ye bağlı sorular ve test sorularındaki cevap istatistikleri
                var loQuestions = lo.QuestionLearningOutcomes
                    .Where(ql => ql.Question.Type == QuestionType.MultipleChoice && ql.Question.IsActive)
                    .Select(ql => ql.Question)
                    .ToList();

                var answers = loQuestions.SelectMany(q => q.StudentAnswers).ToList();
                int loCorrect = answers.Count(a => a.IsCorrect);
                double loRate = answers.Count > 0
                    ? (double)loCorrect / answers.Count * 100
                    : 0;

                report.LinkedLearningOutcomes.Add(new LinkedLearningOutcomeDto
                {
                    LearningOutcomeId = lo.Id,
                    Code = lo.Code,
                    Name = lo.Name,
                    CourseName = lo.Course.Name,
                    ContributionLevel = mapping.ContributionLevel,
                    QuestionCount = loQuestions.Count,
                    SuccessRate = Math.Round(loRate, 1)
                });

                totalCorrect += loCorrect;
                totalAnswered += answers.Count;
                totalQuestions += loQuestions.Count;
            }

            report.TotalQuestionCount = totalQuestions;
            report.TotalAnswerCount = totalAnswered;
            report.OverallSuccessRate = totalAnswered > 0
                ? Math.Round((double)totalCorrect / totalAnswered * 100, 1)
                : 0;

            reports.Add(report);
        }

        return reports.OrderBy(r => r.ProgramOutcomeCode).ToList();
    }

    public async Task LinkToLearningOutcomeAsync(int programOutcomeId, int learningOutcomeId, int contributionLevel)
    {
        var existing = await _context.ProgramOutcomeMappings
            .FirstOrDefaultAsync(m => m.ProgramOutcomeId == programOutcomeId
                                   && m.LearningOutcomeId == learningOutcomeId);

        if (existing != null)
        {
            existing.ContributionLevel = Math.Clamp(contributionLevel, 1, 5);
        }
        else
        {
            _context.ProgramOutcomeMappings.Add(new ProgramOutcomeMapping
            {
                ProgramOutcomeId = programOutcomeId,
                LearningOutcomeId = learningOutcomeId,
                ContributionLevel = Math.Clamp(contributionLevel, 1, 5)
            });
        }
        await _context.SaveChangesAsync();
    }

    public async Task UnlinkFromLearningOutcomeAsync(int programOutcomeId, int learningOutcomeId)
    {
        var link = await _context.ProgramOutcomeMappings
            .FirstOrDefaultAsync(m => m.ProgramOutcomeId == programOutcomeId
                                   && m.LearningOutcomeId == learningOutcomeId);

        if (link != null)
        {
            _context.ProgramOutcomeMappings.Remove(link);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<LearningOutcomeDto>> GetAllLearningOutcomesInProgramAsync(int programId)
    {
        return await _context.LearningOutcomes
            .Include(lo => lo.Course)
            .Where(lo => lo.Course.ProgramId == programId)
            .Select(lo => new LearningOutcomeDto
            {
                Id = lo.Id,
                CourseId = lo.CourseId,
                Code = lo.Code,
                Name = lo.Course.Name + " — " + lo.Code + " " + lo.Name,
                Description = lo.Description,
                TopicCount = lo.TopicLearningOutcomes.Count,
                QuestionCount = lo.QuestionLearningOutcomes.Count,
                ProgramOutcomeCodes = lo.ProgramOutcomeMappings
                    .Select(m => m.ProgramOutcome.Code)
                    .ToList()
            })
            .OrderBy(lo => lo.Name)
            .ToListAsync();
    }

    public async Task<List<MappedLearningOutcomeDto>> GetMappedLearningOutcomesAsync(int programOutcomeId)
    {
        return await _context.ProgramOutcomeMappings
            .Include(m => m.LearningOutcome)
                .ThenInclude(lo => lo.Course)
            .Where(m => m.ProgramOutcomeId == programOutcomeId)
            .Select(m => new MappedLearningOutcomeDto
            {
                LearningOutcomeId = m.LearningOutcomeId,
                Code = m.LearningOutcome.Code,
                Name = m.LearningOutcome.Name,
                CourseName = m.LearningOutcome.Course.Name,
                ContributionLevel = m.ContributionLevel
            })
            .OrderBy(m => m.CourseName).ThenBy(m => m.Code)
            .ToListAsync();
    }
}
