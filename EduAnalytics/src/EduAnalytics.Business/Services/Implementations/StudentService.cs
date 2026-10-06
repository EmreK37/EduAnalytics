using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class StudentService : IStudentService
{
    private readonly EduAnalyticsDbContext _context;

    public StudentService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<List<StudentDto>> GetAllAsync()
    {
        return await _context.Students
            .Select(s => new StudentDto
            {
                Id = s.Id,
                StudentNumber = s.StudentNumber,
                FullName = s.FullName,
                ClassName = s.ClassName,
                EnrolledCourseCount = s.StudentCourses.Count
            })
            .OrderBy(s => s.StudentNumber)
            .ToListAsync();
    }

    public async Task<int> CreateAsync(StudentSaveModel model)
    {
        Validate(model);

        var existing = await _context.Students
            .FirstOrDefaultAsync(s => s.StudentNumber == model.StudentNumber);
        if (existing != null)
            throw new InvalidOperationException($"Bu öğrenci numarası zaten kayıtlı: {model.StudentNumber}");

        var student = new Student
        {
            StudentNumber = model.StudentNumber.Trim(),
            FullName = model.FullName.Trim(),
            ClassName = string.IsNullOrWhiteSpace(model.ClassName) ? "Tanımsız" : model.ClassName.Trim()
        };
        _context.Students.Add(student);
        await _context.SaveChangesAsync();
        return student.Id;
    }

    public async Task UpdateAsync(int id, StudentSaveModel model)
    {
        Validate(model);

        var student = await _context.Students.FindAsync(id)
            ?? throw new InvalidOperationException($"Öğrenci bulunamadı: {id}");

        // Aynı numarayı başka bir öğrenci kullanıyor mu?
        var conflict = await _context.Students
            .FirstOrDefaultAsync(s => s.StudentNumber == model.StudentNumber && s.Id != id);
        if (conflict != null)
            throw new InvalidOperationException($"Bu öğrenci numarası başka bir öğrenciye ait: {model.StudentNumber}");

        student.StudentNumber = model.StudentNumber.Trim();
        student.FullName = model.FullName.Trim();
        student.ClassName = string.IsNullOrWhiteSpace(model.ClassName) ? "Tanımsız" : model.ClassName.Trim();
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var student = await _context.Students
            .Include(s => s.StudentAnswers)
            .Include(s => s.StudentCourses)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new InvalidOperationException($"Öğrenci bulunamadı: {id}");

        if (student.StudentAnswers.Count > 0)
            throw new InvalidOperationException(
                $"Bu öğrencinin sınav cevapları mevcut ({student.StudentAnswers.Count} adet). Önce cevapları silmek gerekir.");

        if (student.StudentCourses.Count > 0)
            _context.StudentCourses.RemoveRange(student.StudentCourses);

        _context.Students.Remove(student);
        await _context.SaveChangesAsync();
    }

    public async Task<StudentImportResult> ImportAsync(List<StudentSaveModel> rows)
    {
        var result = new StudentImportResult();
        if (rows == null || rows.Count == 0) return result;

        var numbers = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.StudentNumber))
            .Select(r => r.StudentNumber.Trim())
            .ToList();

        var existing = await _context.Students
            .Where(s => numbers.Contains(s.StudentNumber))
            .ToDictionaryAsync(s => s.StudentNumber, s => s);

        int rowIndex = 0;
        foreach (var row in rows)
        {
            rowIndex++;

            if (string.IsNullOrWhiteSpace(row.StudentNumber) || string.IsNullOrWhiteSpace(row.FullName))
            {
                result.SkippedCount++;
                result.Warnings.Add($"Satır {rowIndex}: Öğrenci no veya ad-soyad boş — atlandı.");
                continue;
            }

            var num = row.StudentNumber.Trim();
            var name = row.FullName.Trim();
            var cls = string.IsNullOrWhiteSpace(row.ClassName) ? "Tanımsız" : row.ClassName.Trim();

            if (existing.TryGetValue(num, out var current))
            {
                current.FullName = name;
                current.ClassName = cls;
                result.UpdatedCount++;
            }
            else
            {
                _context.Students.Add(new Student
                {
                    StudentNumber = num,
                    FullName = name,
                    ClassName = cls
                });
                result.InsertedCount++;
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    private static void Validate(StudentSaveModel model)
    {
        if (string.IsNullOrWhiteSpace(model.StudentNumber))
            throw new ArgumentException("Öğrenci numarası boş olamaz.", nameof(model));
        if (string.IsNullOrWhiteSpace(model.FullName))
            throw new ArgumentException("Ad-Soyad boş olamaz.", nameof(model));
    }
}
