using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Enums;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

/// <summary>
/// Bir sınavın puan dağılımı için klasik betimsel istatistikler ve
/// iç tutarlılık (Cronbach α) hesaplamalarını üretir.
/// </summary>
public class ExamStatisticsService : IExamStatisticsService
{
    private readonly EduAnalyticsDbContext _context;

    public ExamStatisticsService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<ExamStatisticsDto?> ComputeAsync(int examId, double acceptabilityIndex = 50.0)
    {
        var exam = await _context.Exams
            .Include(e => e.ExamQuestions.Where(eq => !eq.IsCancelled))
                .ThenInclude(eq => eq.Question)
            .FirstOrDefaultAsync(e => e.Id == examId);

        if (exam == null) return null;

        var examQuestions = exam.ExamQuestions.Where(eq => !eq.IsCancelled).ToList();
        if (examQuestions.Count == 0) return null;

        var qIds = examQuestions.Select(eq => eq.QuestionId).ToList();
        var answers = await _context.StudentAnswers
            .Where(sa => sa.ExamId == examId && qIds.Contains(sa.QuestionId))
            .ToListAsync();

        var maxPossible = examQuestions.Sum(eq => eq.OverrideMaxPoints ?? eq.Question.MaxPoints);
        var examQuestionMap = examQuestions.ToDictionary(eq => eq.QuestionId);

        // Öğrenci başına toplam puan
        var studentScores = answers
            .GroupBy(a => a.StudentId)
            .Select(g => (double)g.Sum(a =>
            {
                if (!examQuestionMap.TryGetValue(a.QuestionId, out var eq)) return 0m;
                return ExamAnalysisService.ComputeScore(eq.Question, a, eq.OverrideMaxPoints);
            }))
            .ToList();

        if (studentScores.Count == 0)
        {
            return new ExamStatisticsDto
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                StudentCount = 0,
                MaxPossibleScore = maxPossible,
                AcceptabilityIndex = acceptabilityIndex
            };
        }

        var sorted = studentScores.OrderBy(s => s).ToList();
        int n = sorted.Count;
        double mean = sorted.Average();

        // Median, Q1, Q3
        double median = Percentile(sorted, 50);
        double q1 = Percentile(sorted, 25);
        double q3 = Percentile(sorted, 75);
        double iqr = q3 - q1;

        // Mode (en sık tekrarlayan tam değer; bağda en küçüğü)
        double mode = sorted
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First().Key;

        // Varyans, SD
        double variance = sorted.Sum(x => (x - mean) * (x - mean)) / n;
        double sd = Math.Sqrt(variance);

        // MAD (ortalamadan), MedAD (ortancadan)
        double mad = sorted.Select(x => Math.Abs(x - mean)).Average();
        var devsFromMedian = sorted.Select(x => Math.Abs(x - median)).OrderBy(x => x).ToList();
        double medAD = Percentile(devsFromMedian, 50);

        // Standart hata
        double se = sd / Math.Sqrt(n);

        // Skewness (Fisher-Pearson; n bölmesi)
        double skewness = sd > 0
            ? sorted.Sum(x => Math.Pow((x - mean) / sd, 3)) / n
            : 0;

        // Kurtosis (excess kurtosis: − 3)
        double kurtosis = sd > 0
            ? sorted.Sum(x => Math.Pow((x - mean) / sd, 4)) / n - 3.0
            : 0;

        // BDK (CV = SD/Mean × 100)
        double cv = mean != 0 ? sd / mean * 100 : 0;

        // ─── Cronbach α ───
        // α = (k / (k − 1)) × (1 − Σ Var(item) / Var(total))
        double cronbach = ComputeCronbachAlpha(examQuestions, answers, studentScores, examQuestionMap);

        // ─── Geçme notu ───
        double passingScore = maxPossible > 0 ? (double)maxPossible * acceptabilityIndex / 100.0 : 0;
        int passed = studentScores.Count(s => s >= passingScore);
        int failed = n - passed;
        double passRate = (double)passed / n * 100;

        return new ExamStatisticsDto
        {
            ExamId = exam.Id,
            ExamTitle = exam.Title,
            StudentCount = n,
            MaxPossibleScore = maxPossible,

            Mean = Math.Round(mean, 2),
            Median = Math.Round(median, 2),
            Mode = Math.Round(mode, 2),

            Q1 = Math.Round(q1, 2),
            Q3 = Math.Round(q3, 2),
            InterquartileRange = Math.Round(iqr, 2),
            SemiInterquartileRange = Math.Round(iqr / 2.0, 2),
            QuartileCoefficient = (q3 + q1) > 0 ? Math.Round((q3 - q1) / (q3 + q1), 4) : 0,

            StandardDeviation = Math.Round(sd, 2),
            Variance = Math.Round(variance, 2),
            MeanAbsoluteDeviation = Math.Round(mad, 2),
            MedianAbsoluteDeviation = Math.Round(medAD, 2),
            StandardError = Math.Round(se, 2),
            Range = Math.Round(sorted.Max() - sorted.Min(), 2),
            HighestScore = sorted.Max(),
            LowestScore = sorted.Min(),

            Skewness = Math.Round(skewness, 3),
            Kurtosis = Math.Round(kurtosis, 3),
            CoefficientOfVariation = Math.Round(cv, 2),

            CronbachAlpha = Math.Round(cronbach, 3),
            CronbachAlphaInterpretation = InterpretCronbach(cronbach),

            AcceptabilityIndex = acceptabilityIndex,
            PassingScore = Math.Round(passingScore, 2),
            PassedStudentCount = passed,
            FailedStudentCount = failed,
            PassRate = Math.Round(passRate, 1)
        };
    }

    /// <summary>
    /// Yüzdelik değer (lineer interpolasyon yöntemi).
    /// p ∈ [0, 100]. Sıralı listede konumu: (n − 1) × p / 100.
    /// </summary>
    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        if (sorted.Count == 1) return sorted[0];
        double rank = (sorted.Count - 1) * p / 100.0;
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        if (lo == hi) return sorted[lo];
        double frac = rank - lo;
        return sorted[lo] + frac * (sorted[hi] - sorted[lo]);
    }

    /// <summary>
    /// Cronbach α = (k / (k − 1)) × (1 − Σ Var(item) / Var(total))
    /// k = madde (soru) sayısı.
    /// </summary>
    private static double ComputeCronbachAlpha(
        List<Core.Entities.ExamQuestion> examQuestions,
        List<Core.Entities.StudentAnswer> answers,
        List<double> totalScores,
        Dictionary<int, Core.Entities.ExamQuestion> map)
    {
        int k = examQuestions.Count;
        if (k < 2 || totalScores.Count < 2) return 0;

        // Toplam puan varyansı
        double meanT = totalScores.Average();
        double varT = totalScores.Sum(x => (x - meanT) * (x - meanT)) / totalScores.Count;
        if (varT == 0) return 0;

        // Madde (soru) puan varyanslarının toplamı
        double sumItemVar = 0;
        foreach (var eq in examQuestions)
        {
            var max = eq.OverrideMaxPoints ?? eq.Question.MaxPoints;
            var itemScores = answers
                .Where(a => a.QuestionId == eq.QuestionId)
                .Select(a => (double)ExamAnalysisService.ComputeScore(eq.Question, a, eq.OverrideMaxPoints))
                .ToList();

            if (itemScores.Count < 2) continue;
            double m = itemScores.Average();
            double v = itemScores.Sum(x => (x - m) * (x - m)) / itemScores.Count;
            sumItemVar += v;
        }

        return ((double)k / (k - 1)) * (1.0 - sumItemVar / varT);
    }

    /// <summary>Cronbach α'nın klasik yorumu.</summary>
    private static string InterpretCronbach(double a)
    {
        if (a >= 0.90) return "Mükemmel güvenilirlik";
        if (a >= 0.80) return "Yüksek güvenilirlik";
        if (a >= 0.70) return "Kabul edilebilir";
        if (a >= 0.60) return "Sorgulanabilir";
        if (a >= 0.50) return "Zayıf";
        return "Kabul edilemez";
    }
}
