using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class AcademicStructureService : IAcademicStructureService
{
    private readonly EduAnalyticsDbContext _context;

    public AcademicStructureService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    // ════════════════════════════════════════════════
    // PROGRAMS
    // ════════════════════════════════════════════════

    public async Task<List<ProgramListDto>> GetProgramsAsync()
    {
        return await _context.Programs
            .Select(p => new ProgramListDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
                CourseCount = p.Courses.Count,
                OutcomeCount = p.ProgramOutcomes.Count
            })
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<int> CreateProgramAsync(ProgramSaveModel model)
    {
        ValidateProgram(model);

        var existing = await _context.Programs.FirstOrDefaultAsync(p => p.Code == model.Code);
        if (existing != null)
            throw new InvalidOperationException($"Bu program kodu zaten kayıtlı: {model.Code}");

        var entity = new Program
        {
            Code = model.Code.Trim(),
            Name = model.Name.Trim(),
            Description = model.Description?.Trim()
        };
        _context.Programs.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task UpdateProgramAsync(int id, ProgramSaveModel model)
    {
        ValidateProgram(model);

        var entity = await _context.Programs.FindAsync(id)
            ?? throw new InvalidOperationException($"Program bulunamadı: {id}");

        var conflict = await _context.Programs
            .FirstOrDefaultAsync(p => p.Code == model.Code && p.Id != id);
        if (conflict != null)
            throw new InvalidOperationException($"Bu program kodu başka bir programa ait: {model.Code}");

        entity.Code = model.Code.Trim();
        entity.Name = model.Name.Trim();
        entity.Description = model.Description?.Trim();
        await _context.SaveChangesAsync();
    }

    public async Task DeleteProgramAsync(int id)
    {
        var entity = await _context.Programs
            .Include(p => p.Courses)
            .Include(p => p.ProgramOutcomes)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException($"Program bulunamadı: {id}");

        if (entity.Courses.Count > 0)
            throw new InvalidOperationException(
                $"Bu programa bağlı {entity.Courses.Count} ders var. Önce dersleri silin.");

        if (entity.ProgramOutcomes.Count > 0)
            throw new InvalidOperationException(
                $"Bu programa bağlı {entity.ProgramOutcomes.Count} program çıktısı (PÇ) var. Önce PÇ'leri silin.");

        _context.Programs.Remove(entity);
        await _context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════
    // COURSES
    // ════════════════════════════════════════════════

    public async Task<List<CourseListDto>> GetCoursesByProgramAsync(int programId)
    {
        return await _context.Courses
            .Where(c => c.ProgramId == programId)
            .Select(c => new CourseListDto
            {
                Id = c.Id,
                ProgramId = c.ProgramId,
                Code = c.Code,
                Name = c.Name,
                Description = c.Description,
                TopicCount = c.Topics.Count,
                LearningOutcomeCount = c.LearningOutcomes.Count,
                QuestionCount = c.Questions.Count,
                ExamCount = c.Exams.Count
            })
            .OrderBy(c => c.Code)
            .ToListAsync();
    }

    public async Task<int> CreateCourseAsync(CourseSaveModel model, int createdByUserId)
    {
        ValidateCourse(model);

        var existing = await _context.Courses.FirstOrDefaultAsync(c => c.Code == model.Code);
        if (existing != null)
            throw new InvalidOperationException($"Bu ders kodu zaten kayıtlı: {model.Code}");

        var programExists = await _context.Programs.AnyAsync(p => p.Id == model.ProgramId);
        if (!programExists)
            throw new InvalidOperationException("Program bulunamadı.");

        var entity = new Course
        {
            ProgramId = model.ProgramId,
            Code = model.Code.Trim(),
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            CreatedByUserId = createdByUserId
        };
        _context.Courses.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task UpdateCourseAsync(int id, CourseSaveModel model)
    {
        ValidateCourse(model);

        var entity = await _context.Courses.FindAsync(id)
            ?? throw new InvalidOperationException($"Ders bulunamadı: {id}");

        var conflict = await _context.Courses
            .FirstOrDefaultAsync(c => c.Code == model.Code && c.Id != id);
        if (conflict != null)
            throw new InvalidOperationException($"Bu ders kodu başka bir derse ait: {model.Code}");

        entity.Code = model.Code.Trim();
        entity.Name = model.Name.Trim();
        entity.Description = model.Description?.Trim();
        // ProgramId değiştirme bilinçli olarak desteklenmiyor — bağımlı veriler bozulabilir.

        await _context.SaveChangesAsync();
    }

    public async Task DeleteCourseAsync(int id)
    {
        var entity = await _context.Courses
            .Include(c => c.Topics)
            .Include(c => c.LearningOutcomes)
            .Include(c => c.Questions)
            .Include(c => c.Exams)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException($"Ders bulunamadı: {id}");

        if (entity.LearningOutcomes.Count > 0)
            throw new InvalidOperationException(
                $"Bu derse bağlı {entity.LearningOutcomes.Count} ÖÇ var. Önce ÖÇ'leri silin.");

        if (entity.Questions.Count > 0)
            throw new InvalidOperationException(
                $"Bu derse bağlı {entity.Questions.Count} soru var. Önce soruları silin.");

        if (entity.Exams.Count > 0)
            throw new InvalidOperationException(
                $"Bu derse bağlı {entity.Exams.Count} sınav var. Önce sınavları silin.");

        // Konular varsa otomatik silinir (cascade), ek bağımlılıkları engellenmiş durumda.
        _context.Courses.Remove(entity);
        await _context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════
    // TOPICS
    // ════════════════════════════════════════════════

    public async Task<List<TopicListDto>> GetTopicsByCourseAsync(int courseId)
    {
        return await _context.Topics
            .Where(t => t.CourseId == courseId)
            .Select(t => new TopicListDto
            {
                Id = t.Id,
                CourseId = t.CourseId,
                WeekNumber = t.WeekNumber,
                Title = t.Title,
                Description = t.Description,
                LearningOutcomeCount = t.TopicLearningOutcomes.Count,
                QuestionCount = t.QuestionTopics.Count
            })
            .OrderBy(t => t.WeekNumber)
            .ToListAsync();
    }

    public async Task<int> CreateTopicAsync(TopicSaveModel model)
    {
        ValidateTopic(model);

        var courseExists = await _context.Courses.AnyAsync(c => c.Id == model.CourseId);
        if (!courseExists)
            throw new InvalidOperationException("Ders bulunamadı.");

        var entity = new Topic
        {
            CourseId = model.CourseId,
            WeekNumber = model.WeekNumber,
            Title = model.Title.Trim(),
            Description = model.Description?.Trim()
        };
        _context.Topics.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task UpdateTopicAsync(int id, TopicSaveModel model)
    {
        ValidateTopic(model);

        var entity = await _context.Topics.FindAsync(id)
            ?? throw new InvalidOperationException($"Konu bulunamadı: {id}");

        entity.WeekNumber = model.WeekNumber;
        entity.Title = model.Title.Trim();
        entity.Description = model.Description?.Trim();
        await _context.SaveChangesAsync();
    }

    public async Task DeleteTopicAsync(int id)
    {
        var entity = await _context.Topics
            .Include(t => t.TopicLearningOutcomes)
            .Include(t => t.QuestionTopics)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"Konu bulunamadı: {id}");

        if (entity.TopicLearningOutcomes.Count > 0)
            throw new InvalidOperationException(
                $"Bu konuya bağlı {entity.TopicLearningOutcomes.Count} ÖÇ var. Önce ÖÇ bağlantılarını kaldırın.");

        if (entity.QuestionTopics.Count > 0)
            throw new InvalidOperationException(
                $"Bu konuya bağlı {entity.QuestionTopics.Count} soru var. Önce soru bağlantılarını kaldırın.");

        _context.Topics.Remove(entity);
        await _context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════
    // VALIDATION
    // ════════════════════════════════════════════════

    private static void ValidateProgram(ProgramSaveModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Code))
            throw new ArgumentException("Program kodu boş olamaz.", nameof(model));
        if (string.IsNullOrWhiteSpace(model.Name))
            throw new ArgumentException("Program adı boş olamaz.", nameof(model));
    }

    private static void ValidateCourse(CourseSaveModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Code))
            throw new ArgumentException("Ders kodu boş olamaz.", nameof(model));
        if (string.IsNullOrWhiteSpace(model.Name))
            throw new ArgumentException("Ders adı boş olamaz.", nameof(model));
        if (model.ProgramId <= 0)
            throw new ArgumentException("Geçerli bir program seçmelisiniz.", nameof(model));
    }

    private static void ValidateTopic(TopicSaveModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
            throw new ArgumentException("Konu başlığı boş olamaz.", nameof(model));
        if (model.WeekNumber < 1 || model.WeekNumber > 52)
            throw new ArgumentException("Hafta numarası 1-52 arasında olmalı.", nameof(model));
        if (model.CourseId <= 0)
            throw new ArgumentException("Geçerli bir ders seçmelisiniz.", nameof(model));
    }
}
