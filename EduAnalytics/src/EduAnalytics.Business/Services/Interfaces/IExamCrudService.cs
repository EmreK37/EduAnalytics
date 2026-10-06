using EduAnalytics.Business.Dtos;
using EduAnalytics.Core.Entities;

namespace EduAnalytics.Business.Services.Interfaces;

/// <summary>
/// Sınav/Soru CRUD işlemleri.
/// </summary>
public interface IExamCrudService
{
    Task<List<Course>> GetCoursesAsync();
    Task<List<Course>> GetCoursesByProgramAsync(int programId);
    Task<List<Topic>> GetTopicsForCourseAsync(int courseId);
    Task<List<Student>> GetStudentsAsync();

    /// <summary>
    /// Yeni sorularla yeni sınav oluşturur. Sorular soru bankasına eklenir + ExamQuestion ile sınava bağlanır.
    /// </summary>
    Task<int> CreateExamAsync(ExamCreateModel model);

    /// <summary>
    /// Soru bankasından seçilmiş sorularla yeni sınav oluşturur. Yeni soru üretmez, sadece bağlar.
    /// </summary>
    Task<int> CreateExamFromBankAsync(ExamFromBankCreateModel model);

    /// <summary>
    /// Sistemdeki ilk (default) öğretmen kullanıcısının Id'si.
    /// İleride login sistemi eklendiğinde oturumdaki kullanıcıdan alınacak.
    /// </summary>
    Task<int> GetDefaultUserIdAsync();

    /// <summary>Tüm sınavları yönetim ekranı için özet kart bilgisiyle döner.</summary>
    Task<List<ExamListItemDto>> GetAllExamsAsync();

    /// <summary>Tek bir sınavın temel alanlarını düzenleme formu için döner.</summary>
    Task<ExamUpdateModel?> GetExamForEditAsync(int examId);

    /// <summary>Sınavın başlık, tarih, süre ve tip alanlarını günceller.</summary>
    Task UpdateExamAsync(ExamUpdateModel model);

    /// <summary>Sınavı ve ilişkili alt kayıtları cascade ile siler.</summary>
    Task DeleteExamAsync(int examId);
}
