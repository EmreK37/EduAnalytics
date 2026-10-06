namespace EduAnalytics.Core.Enums;

/// <summary>
/// Soru tipini belirtir.
/// </summary>
public enum QuestionType
{
    /// <summary>Çoktan seçmeli (A/B/C/D). Otomatik notlandırılır, çeldirici analizi yapılır.</summary>
    MultipleChoice = 1,

    /// <summary>Klasik (açık uçlu). Öğretmen manuel puan verir, çeldirici analizi uygulanmaz.</summary>
    OpenEnded = 2
}
