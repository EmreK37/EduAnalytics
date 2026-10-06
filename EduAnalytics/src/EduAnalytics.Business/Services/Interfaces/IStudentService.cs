using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

/// <summary>
/// Öğrenci yönetimi (CRUD + toplu yükleme).
/// </summary>
public interface IStudentService
{
    Task<List<StudentDto>> GetAllAsync();
    Task<int> CreateAsync(StudentSaveModel model);
    Task UpdateAsync(int id, StudentSaveModel model);
    Task DeleteAsync(int id);

    /// <summary>
    /// Excel'den okunan öğrenci satırlarını sisteme yükler.
    /// Mevcut StudentNumber → Update, yeni → Insert. Boş satırlar atlanır.
    /// </summary>
    Task<StudentImportResult> ImportAsync(List<StudentSaveModel> rows);
}
