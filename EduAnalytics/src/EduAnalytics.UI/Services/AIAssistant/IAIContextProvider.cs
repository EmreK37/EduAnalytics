namespace EduAnalytics.UI.Services.AIAssistant;

/// <summary>
/// View modeller bunu implemente ederek AI'a göndermek istedikleri özet bağlamı sunar.
/// PII gönderme; sadece istatistik/özet ver.
/// </summary>
public interface IAIContextProvider
{
    /// <summary>Aktif ekran/sınav/soru özeti. Null/boş dönülürse generic bağlam kullanılır.</summary>
    string? GetAIContext();
}
