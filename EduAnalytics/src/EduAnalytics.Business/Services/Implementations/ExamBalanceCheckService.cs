using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using EduAnalytics.Core.Enums;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

/// <summary>
/// Sınav soru dağılımının dengeli olup olmadığını kontrol eder ve uyarılar üretir.
/// Tetikleyiciler:
///   • Tek bir öğrenim çıktısı soruların %50'sinden fazlasını kapsıyorsa → Yığılma uyarısı.
///   • Dersin bir ÖÇ'sü hiç sorulmamışsa → Eksik kapsam uyarısı.
///   • Test/klasik dengesi 0 ise (örn. hepsi test) → Tip dengesizliği uyarısı.
///   • Dağılım eşitsizliği (Gini) > 0.5 ise → Genel dengesizlik.
/// </summary>
public class ExamBalanceCheckService : IExamBalanceCheckService
{
    private readonly EduAnalyticsDbContext _context;

    public ExamBalanceCheckService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<ExamBalanceReportDto> AnalyzeAsync(int examId)
    {
        var exam = await _context.Exams
            .Include(e => e.ExamQuestions.Where(eq => !eq.IsCancelled))
                .ThenInclude(eq => eq.Question)
                    .ThenInclude(q => q.QuestionLearningOutcomes).ThenInclude(ql => ql.LearningOutcome)
            .FirstOrDefaultAsync(e => e.Id == examId)
            ?? throw new InvalidOperationException($"Sınav bulunamadı: {examId}");

        var questions = exam.ExamQuestions.Where(eq => !eq.IsCancelled).Select(eq => eq.Question).ToList();
        return BuildReport(exam.Id, exam.Title, exam.CourseId, questions);
    }

    public async Task<ExamBalanceReportDto> AnalyzeDraftAsync(int courseId, List<int> questionIds)
    {
        var questions = await _context.Questions
            .Include(q => q.QuestionLearningOutcomes).ThenInclude(ql => ql.LearningOutcome)
            .Where(q => questionIds.Contains(q.Id))
            .ToListAsync();

        return BuildReport(0, "(Taslak)", courseId, questions);
    }

    private ExamBalanceReportDto BuildReport(int examId, string title, int courseId, List<Question> questions)
    {
        var report = new ExamBalanceReportDto
        {
            ExamId = examId,
            ExamTitle = title,
            TotalQuestions = questions.Count
        };

        if (questions.Count == 0)
        {
            report.Warnings.Add(new BalanceWarningDto
            {
                WarningType = "MissingOutcome",
                Severity = "Critical",
                Message = "Sınava hiç soru eklenmedi."
            });
            return report;
        }

        var loCoverage = questions
            .SelectMany(q => q.QuestionLearningOutcomes.Select(ql => ql.LearningOutcome))
            .GroupBy(lo => lo.Id)
            .Select(g => new
            {
                LO = g.First(),
                Count = g.Count()
            })
            .ToList();

        foreach (var lc in loCoverage)
        {
            report.LearningOutcomeCoverage.Add(new LearningOutcomeCoverageDto
            {
                LearningOutcomeId = lc.LO.Id,
                Code = lc.LO.Code,
                Name = lc.LO.Name,
                QuestionCount = lc.Count,
                Percentage = Math.Round((double)lc.Count / questions.Count * 100, 1)
            });
        }

        // ─── Uyarı kuralları ───
        // 1) Tek öğrenim çıktısı yoğunluğu
        var dominantOutcome = report.LearningOutcomeCoverage
            .Where(t => t.Percentage > 50)
            .OrderByDescending(t => t.Percentage)
            .FirstOrDefault();

        if (dominantOutcome != null)
        {
            report.Warnings.Add(new BalanceWarningDto
            {
                WarningType = "Concentration",
                Severity = "Warning",
                Message = $"Soruların %{dominantOutcome.Percentage:0.0}'i tek öğrenim çıktısına yığılmış: " +
                          $"{dominantOutcome.Code} - {dominantOutcome.Name}."
            });
        }

        // 2) Sadece 1-2 öğrenim çıktısına yığılmış mı?
        if (report.LearningOutcomeCoverage.Count <= 2 && questions.Count >= 4)
        {
            var outcomes = string.Join(", ", report.LearningOutcomeCoverage.Select(t => t.Code).Distinct());
            report.Warnings.Add(new BalanceWarningDto
            {
                WarningType = "Concentration",
                Severity = "Warning",
                Message = $"Sorular sadece şu öğrenim çıktılarında yoğunlaşmış: {outcomes}. Daha geniş ÖÇ kapsamı önerilir."
            });
        }

        // 3) Dersin tüm ÖÇ'leri vs. kapsanan ÖÇ'ler
        var allCourseOutcomes = _context.LearningOutcomes
            .Where(lo => lo.CourseId == courseId)
            .Select(lo => new { lo.Id, lo.Code })
            .ToList();

        var coveredIds = loCoverage.Select(lc => lc.LO.Id).ToHashSet();
        var missingOutcomes = allCourseOutcomes.Where(lo => !coveredIds.Contains(lo.Id)).ToList();

        if (missingOutcomes.Count > 0 && allCourseOutcomes.Count > 0)
        {
            // Vize sınavıysa eksik ÖÇ kabul edilebilir, sadece info
            var severity = missingOutcomes.Count >= allCourseOutcomes.Count / 2 ? "Warning" : "Info";
            var codes = string.Join(", ", missingOutcomes.Take(5).Select(o => o.Code));
            var more = missingOutcomes.Count > 5 ? $" (+{missingOutcomes.Count - 5} daha)" : "";

            report.Warnings.Add(new BalanceWarningDto
            {
                WarningType = "MissingOutcome",
                Severity = severity,
                Message = $"Dersin {missingOutcomes.Count} ÖÇ'sü hiç sorulmamış: {codes}{more}."
            });
        }

        // 4) Tip dengesi (test vs. klasik)
        var mcCount = questions.Count(q => q.Type == QuestionType.MultipleChoice);
        var oeCount = questions.Count - mcCount;
        if (questions.Count >= 6)
        {
            if (oeCount == 0)
                report.Warnings.Add(new BalanceWarningDto
                {
                    WarningType = "TypeImbalance",
                    Severity = "Info",
                    Message = "Sınavda hiç klasik soru yok. Üst düzey beceriler ölçülmeyebilir."
                });
            else if (mcCount == 0)
                report.Warnings.Add(new BalanceWarningDto
                {
                    WarningType = "TypeImbalance",
                    Severity = "Info",
                    Message = "Sınavda hiç test sorusu yok. Hızlı tarama eksik kalabilir."
                });
        }

        // ─── Skorlama: Gini katsayısı ile dağılım eşitsizliği ───
        var gini = ComputeGini(report.LearningOutcomeCoverage.Select(t => (double)t.QuestionCount).ToList());
        report.DistributionInequality = Math.Round(gini, 3);
        report.BalanceScore = Math.Round((1.0 - gini) * 100, 1);

        if (gini > 0.5)
        {
            report.Warnings.Add(new BalanceWarningDto
            {
                WarningType = "Concentration",
                Severity = gini > 0.7 ? "Critical" : "Warning",
                Message = $"Genel dağılım dengesizliği yüksek (Gini: {gini:0.00}). Soruları daha eşit dağıtmayı düşünün."
            });
        }

        return report;
    }

    /// <summary>
    /// Gini katsayısı: 0 = tam eşit dağılım, 1 = bir noktaya yığılmış.
    /// Sınav dengesi için sezgisel bir ölçü.
    /// </summary>
    private static double ComputeGini(List<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        int n = sorted.Count;
        double sum = sorted.Sum();
        if (sum == 0) return 0;

        double cumulative = 0;
        for (int i = 0; i < n; i++)
            cumulative += (i + 1) * sorted[i];

        return (2.0 * cumulative) / (n * sum) - (n + 1.0) / n;
    }
}
