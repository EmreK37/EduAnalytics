using EduAnalytics.Business.Dtos;
using EduAnalytics.Core.Enums;

namespace EduAnalytics.Business.Services.Interfaces;

/// <summary>
/// Kitapçık üretimi ve şık permütasyon haritasını çözme.
/// </summary>
public interface IExamBookletService
{
    Task<List<BookletDto>> GetBookletsForExamAsync(int examId);
    Task<BookletDto?> GetBookletAsync(int bookletId);

    /// <summary>Sınav için yeniden kitapçık üretir (mevcutları siler ve yeniden oluşturur).</summary>
    Task RegenerateBookletsAsync(int examId, int bookletCount, bool shuffleOptions);

    /// <summary>
    /// Öğrencinin kitapçıkta seçtiği şık (örn. "C") → orijinal sorudaki şıkka çevirir.
    /// Kitapçık karıştırılmadıysa aynı harf döner.
    /// </summary>
    OptionLetter DecodeStudentChoice(string? optionShuffleMap, OptionLetter displayedChoice);

    /// <summary>Şık permütasyon haritasını ters yönlü çözer (orijinal harfi bul).</summary>
    string? GetReverseMap(string? optionShuffleMap);
}
