namespace EduAnalytics.Core.Enums;

/// <summary>
/// Sınav tipi. Vize seçilirse soru havuzu o tarihe kadar olan ÖÇ'lerle sınırlanır.
/// </summary>
public enum ExamType
{
    Quiz = 1,
    Midterm = 2,
    Final = 3,
    MakeUp = 4
}
