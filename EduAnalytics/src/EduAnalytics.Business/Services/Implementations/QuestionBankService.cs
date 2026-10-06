using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class QuestionBankService : IQuestionBankService
{
    private readonly EduAnalyticsDbContext _context;

    public QuestionBankService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<List<QuestionBankItemDto>> SearchAsync(QuestionBankFilter filter)
    {
        var query = _context.Questions
            .Include(q => q.Course)
            .Include(q => q.QuestionTopics).ThenInclude(qt => qt.Topic)
            .Include(q => q.QuestionLearningOutcomes).ThenInclude(ql => ql.LearningOutcome)
            .Include(q => q.ExamQuestions).ThenInclude(eq => eq.Exam)
            .AsNoTracking()
            .AsQueryable();

        if (filter.CourseId.HasValue)
            query = query.Where(q => q.CourseId == filter.CourseId.Value);

        if (filter.Type.HasValue)
            query = query.Where(q => q.Type == filter.Type.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(q => q.IsActive == filter.IsActive.Value);

        if (filter.IsFavorite.HasValue)
            query = query.Where(q => q.IsFavorite == filter.IsFavorite.Value);

        if (filter.LearningOutcomeIds != null && filter.LearningOutcomeIds.Count > 0)
            query = query.Where(q => q.QuestionLearningOutcomes.Any(ql => filter.LearningOutcomeIds.Contains(ql.LearningOutcomeId)));

        if (filter.TopicIds != null && filter.TopicIds.Count > 0)
            query = query.Where(q => q.QuestionTopics.Any(qt => filter.TopicIds.Contains(qt.TopicId)));

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var s = filter.SearchText.Trim();
            query = query.Where(q => EF.Functions.Like(q.QuestionText, $"%{s}%"));
        }

        var questions = await query.OrderByDescending(q => q.CreatedAt).ToListAsync();

        return questions.Select(MapToItem).ToList();
    }

    public async Task<QuestionBankItemDto?> GetByIdAsync(int questionId)
    {
        var q = await _context.Questions
            .Include(qq => qq.Course)
            .Include(qq => qq.QuestionTopics).ThenInclude(qt => qt.Topic)
            .Include(qq => qq.QuestionLearningOutcomes).ThenInclude(ql => ql.LearningOutcome)
            .Include(qq => qq.ExamQuestions).ThenInclude(eq => eq.Exam)
            .AsNoTracking()
            .FirstOrDefaultAsync(qq => qq.Id == questionId);

        return q == null ? null : MapToItem(q);
    }

    public async Task<QuestionBankCreateModel?> GetEditModelAsync(int questionId)
    {
        var q = await _context.Questions
            .Include(qq => qq.QuestionTopics)
            .Include(qq => qq.QuestionLearningOutcomes)
            .FirstOrDefaultAsync(qq => qq.Id == questionId);

        if (q == null) return null;

        return new QuestionBankCreateModel
        {
            CourseId = q.CourseId,
            QuestionGroupId = q.QuestionGroupId,
            Type = q.Type,
            MaxPoints = q.MaxPoints,
            QuestionText = q.QuestionText,
            OptionA = q.OptionA,
            OptionB = q.OptionB,
            OptionC = q.OptionC,
            OptionD = q.OptionD,
            OptionE = q.OptionE,
            CorrectOption = q.CorrectOption,
            AnswerKey = q.AnswerKey,
            IsActive = q.IsActive,
            IsFavorite = q.IsFavorite,
            CreatedByUserId = q.CreatedByUserId,
            TopicIds = q.QuestionTopics.Select(qt => qt.TopicId).ToList(),
            LearningOutcomeIds = q.QuestionLearningOutcomes.Select(ql => ql.LearningOutcomeId).ToList()
        };
    }

    public async Task<int> CreateAsync(QuestionBankCreateModel model)
    {
        ValidateCreate(model);

        var q = new Question
        {
            CourseId = model.CourseId,
            QuestionGroupId = model.QuestionGroupId,
            Type = model.Type,
            MaxPoints = model.MaxPoints,
            QuestionText = model.QuestionText.Trim(),
            OptionA = model.OptionA ?? string.Empty,
            OptionB = model.OptionB ?? string.Empty,
            OptionC = model.OptionC ?? string.Empty,
            OptionD = model.OptionD ?? string.Empty,
            OptionE = model.OptionE ?? string.Empty,
            CorrectOption = model.CorrectOption,
            AnswerKey = model.AnswerKey,
            IsActive = model.IsActive,
            IsFavorite = model.IsFavorite,
            CreatedByUserId = model.CreatedByUserId,
            CreatedAt = DateTime.UtcNow
        };
        _context.Questions.Add(q);
        await _context.SaveChangesAsync();

        foreach (var tid in model.TopicIds.Distinct())
            _context.QuestionTopics.Add(new QuestionTopic { QuestionId = q.Id, TopicId = tid });

        foreach (var lid in model.LearningOutcomeIds.Distinct())
            _context.QuestionLearningOutcomes.Add(new QuestionLearningOutcome { QuestionId = q.Id, LearningOutcomeId = lid });

        await _context.SaveChangesAsync();
        return q.Id;
    }

    public async Task UpdateAsync(int questionId, QuestionBankCreateModel model)
    {
        ValidateCreate(model);

        var q = await _context.Questions
            .Include(qq => qq.QuestionTopics)
            .Include(qq => qq.QuestionLearningOutcomes)
            .FirstOrDefaultAsync(qq => qq.Id == questionId)
            ?? throw new InvalidOperationException($"Soru bulunamadı: {questionId}");

        q.CourseId = model.CourseId;
        q.QuestionGroupId = model.QuestionGroupId;
        q.Type = model.Type;
        q.MaxPoints = model.MaxPoints;
        q.QuestionText = model.QuestionText.Trim();
        q.OptionA = model.OptionA ?? string.Empty;
        q.OptionB = model.OptionB ?? string.Empty;
        q.OptionC = model.OptionC ?? string.Empty;
        q.OptionD = model.OptionD ?? string.Empty;
        q.OptionE = model.OptionE ?? string.Empty;
        q.CorrectOption = model.CorrectOption;
        q.AnswerKey = model.AnswerKey;
        q.IsActive = model.IsActive;
        q.IsFavorite = model.IsFavorite;

        // Topic ve LO bağlantılarını yeniden kur
        _context.QuestionTopics.RemoveRange(q.QuestionTopics);
        _context.QuestionLearningOutcomes.RemoveRange(q.QuestionLearningOutcomes);
        await _context.SaveChangesAsync();

        foreach (var tid in model.TopicIds.Distinct())
            _context.QuestionTopics.Add(new QuestionTopic { QuestionId = q.Id, TopicId = tid });

        foreach (var lid in model.LearningOutcomeIds.Distinct())
            _context.QuestionLearningOutcomes.Add(new QuestionLearningOutcome { QuestionId = q.Id, LearningOutcomeId = lid });

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int questionId)
    {
        var usedInExam = await _context.ExamQuestions.AnyAsync(eq => eq.QuestionId == questionId);
        if (usedInExam)
            throw new InvalidOperationException("Bu soru bir sınavda kullanılmış. Önce sınavdan çıkarın veya pasifleştirin.");

        var q = await _context.Questions.FindAsync(questionId)
            ?? throw new InvalidOperationException($"Soru bulunamadı: {questionId}");

        _context.Questions.Remove(q);
        await _context.SaveChangesAsync();
    }

    public async Task ToggleActiveAsync(int questionId)
    {
        var q = await _context.Questions.FindAsync(questionId)
            ?? throw new InvalidOperationException($"Soru bulunamadı: {questionId}");

        q.IsActive = !q.IsActive;
        await _context.SaveChangesAsync();
    }

    public async Task ToggleFavoriteAsync(int questionId)
    {
        var q = await _context.Questions.FindAsync(questionId)
            ?? throw new InvalidOperationException($"Soru bulunamadı: {questionId}");

        q.IsFavorite = !q.IsFavorite;
        await _context.SaveChangesAsync();
    }

    public async Task<int> CreateGroupAsync(QuestionGroupCreateModel model)
    {
        if (string.IsNullOrWhiteSpace(model.StemText))
            throw new ArgumentException("Gövde metni boş olamaz.", nameof(model));

        var g = new QuestionGroup
        {
            CourseId = model.CourseId,
            StemText = model.StemText.Trim(),
            MediaPath = string.IsNullOrWhiteSpace(model.MediaPath) ? null : model.MediaPath.Trim(),
            CreatedByUserId = model.CreatedByUserId,
            CreatedAt = DateTime.UtcNow
        };
        _context.QuestionGroups.Add(g);
        await _context.SaveChangesAsync();
        return g.Id;
    }

    public async Task<List<QuestionGroupDto>> GetGroupsForCourseAsync(int courseId)
    {
        return await _context.QuestionGroups
            .Where(g => g.CourseId == courseId)
            .Select(g => new QuestionGroupDto
            {
                Id = g.Id,
                CourseId = g.CourseId,
                StemText = g.StemText,
                MediaPath = g.MediaPath,
                QuestionCount = g.Questions.Count,
                CreatedAt = g.CreatedAt
            })
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<QuestionBankItemDto>> GetQuestionsForGroupAsync(int groupId)
    {
        var questions = await _context.Questions
            .Include(q => q.Course)
            .Include(q => q.QuestionTopics).ThenInclude(qt => qt.Topic)
            .Include(q => q.QuestionLearningOutcomes).ThenInclude(ql => ql.LearningOutcome)
            .Include(q => q.ExamQuestions).ThenInclude(eq => eq.Exam)
            .Where(q => q.QuestionGroupId == groupId)
            .AsNoTracking()
            .OrderBy(q => q.CreatedAt)
            .ToListAsync();

        return questions.Select(MapToItem).ToList();
    }

    private static QuestionBankItemDto MapToItem(Question q) => new()
    {
        Id = q.Id,
        CourseId = q.CourseId,
        CourseName = q.Course?.Name ?? string.Empty,
        QuestionText = q.QuestionText,
        Type = q.Type,
        MaxPoints = q.MaxPoints,
        CorrectOption = q.CorrectOption,
        IsActive = q.IsActive,
        IsFavorite = q.IsFavorite,
        QuestionGroupId = q.QuestionGroupId,
        LearningOutcomeCodes = q.QuestionLearningOutcomes
            .Select(ql => ql.LearningOutcome.Code)
            .OrderBy(c => c)
            .ToList(),
        TopicWeeks = q.QuestionTopics
            .Select(qt => qt.Topic.WeekNumber)
            .OrderBy(w => w)
            .Distinct()
            .ToList(),
        UsedInExamCount = q.ExamQuestions.Count,
        UsedInExamTitles = q.ExamQuestions
            .Where(eq => eq.Exam != null)
            .Select(eq => eq.Exam.Title)
            .Distinct()
            .OrderBy(t => t)
            .ToList(),
        CreatedAt = q.CreatedAt
    };

    private static void ValidateCreate(QuestionBankCreateModel model)
    {
        if (string.IsNullOrWhiteSpace(model.QuestionText))
            throw new ArgumentException("Soru metni boş olamaz.", nameof(model));
        if (model.MaxPoints <= 0)
            throw new ArgumentException("Puan 0'dan büyük olmalı.", nameof(model));
    }
}
