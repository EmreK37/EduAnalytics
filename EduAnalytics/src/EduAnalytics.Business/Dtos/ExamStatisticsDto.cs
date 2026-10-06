namespace EduAnalytics.Business.Dtos;

/// <summary>
/// Bir sınavın puan dağılımına ait gelişmiş istatistikleri içerir.
/// Aritmetik ortalama, ortanca, çeyrekler, dağılım ölçüleri (SD/MAD/MedAD/SE),
/// şekil ölçüleri (çarpıklık/basıklık), iç tutarlılık (Cronbach α) ve
/// kabul edilebilirlik indeksi → geçme notu hesaplamasını barındırır.
/// </summary>
public class ExamStatisticsDto
{
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = null!;
    public int StudentCount { get; set; }
    public decimal MaxPossibleScore { get; set; }

    // ─── Merkezi eğilim ölçüleri ───
    public double Mean { get; set; }            // Aritmetik ortalama
    public double Median { get; set; }          // Ortanca
    public double Mode { get; set; }            // Tepe değer (en sık tekrar eden puan)

    // ─── Konum ölçüleri (çeyrekler) ───
    public double Q1 { get; set; }                       // 1. çeyrek
    public double Q3 { get; set; }                       // 3. çeyrek
    public double InterquartileRange { get; set; }       // Q3 - Q1
    public double SemiInterquartileRange { get; set; }   // (Q3 - Q1) / 2  → çeyrek kayma
    public double QuartileCoefficient { get; set; }      // (Q3 - Q1) / (Q3 + Q1) → çeyrek kayma değ. katsayısı

    // ─── Dağılım (yayılım) ölçüleri ───
    public double StandardDeviation { get; set; }        // σ
    public double Variance { get; set; }                 // σ²
    public double MeanAbsoluteDeviation { get; set; }    // (1/n) Σ |x − x̄|
    public double MedianAbsoluteDeviation { get; set; }  // medyan |x − medyan|
    public double StandardError { get; set; }            // σ / √n
    public double Range { get; set; }                    // max − min
    public double HighestScore { get; set; }
    public double LowestScore { get; set; }

    // ─── Şekil ölçüleri ───
    public double Skewness { get; set; }     // Çarpıklık (Fisher-Pearson)
    public double Kurtosis { get; set; }     // Basıklık (excess kurtosis)
    public double CoefficientOfVariation { get; set; }   // BDK = (σ / x̄) × 100

    // ─── İç tutarlılık ───
    public double CronbachAlpha { get; set; }            // 0..1 arası güvenilirlik
    public string CronbachAlphaInterpretation { get; set; } = string.Empty;

    // ─── Geçme notu ───
    /// <summary>0–100 arası kabul edilebilirlik indeksi (örn. 50 = %50 ve üzeri geçer).</summary>
    public double AcceptabilityIndex { get; set; }
    /// <summary>Kabul edilebilirlik × MaxPossibleScore / 100.</summary>
    public double PassingScore { get; set; }
    public int PassedStudentCount { get; set; }
    public int FailedStudentCount { get; set; }
    public double PassRate { get; set; }
}
