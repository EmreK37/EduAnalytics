using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Enums;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

/// <summary>
/// Klasik test geliştirme literatüründeki "madde analizi"ni uygular.
///
/// Hesaplanan indeksler:
///   • Zorluk indeksi   p  = doğru / toplam              (0–1)
///   • Ayırt edicilik   D  = (Pü − Pa) / Ng              (−1 .. +1)
///   • Madde güvenilirliği r_jx = D × √(p · (1 − p))
///   • Çeldirici etkinliği — her çeldiricinin seçilme oranı ideal orana yakınlık
///
/// Üst/alt gruplar Truman Kelley'nin %27 kuralına göre belirlenir.
/// </summary>
public class ItemAnalysisService : IItemAnalysisService
{
    private const double UpperLowerRatio = 0.27;
    private readonly EduAnalyticsDbContext _context;

    public ItemAnalysisService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<List<ItemAnalysisDto>> AnalyzeAsync(int examId)
    {
        // Sadece test (çoktan seçmeli) soruları madde analizine girer.
        var examQuestions = await _context.ExamQuestions
            .Where(eq => eq.ExamId == examId
                      && !eq.IsCancelled
                      && eq.Question.Type == QuestionType.MultipleChoice)
            .Include(eq => eq.Question)
            .OrderBy(eq => eq.OrderInExam)
            .ToListAsync();

        if (examQuestions.Count == 0) return new List<ItemAnalysisDto>();

        var qIds = examQuestions.Select(eq => eq.QuestionId).ToList();
        var answers = await _context.StudentAnswers
            .Where(sa => sa.ExamId == examId && qIds.Contains(sa.QuestionId))
            .ToListAsync();

        var examQuestionMap = examQuestions.ToDictionary(eq => eq.QuestionId);

        // Öğrenci toplam puanları (üst/alt grup belirlemek için)
        var studentTotals = answers
            .GroupBy(a => a.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Total = g.Sum(a =>
                {
                    if (!examQuestionMap.TryGetValue(a.QuestionId, out var eq)) return 0m;
                    return ExamAnalysisService.ComputeScore(eq.Question, a, eq.OverrideMaxPoints);
                })
            })
            .OrderByDescending(s => s.Total)
            .ToList();

        if (studentTotals.Count < 4)
        {
            // Kelley %27'sini sağlayacak veri yok → üst/alt analizi anlamsız.
            return BuildBasicAnalysis(examQuestions, answers);
        }

        int groupSize = Math.Max(1, (int)Math.Round(studentTotals.Count * UpperLowerRatio));
        var upperGroup = studentTotals.Take(groupSize).Select(s => s.StudentId).ToHashSet();
        var lowerGroup = studentTotals.TakeLast(groupSize).Select(s => s.StudentId).ToHashSet();

        var answerLookup = answers.ToLookup(a => a.QuestionId);
        var results = new List<ItemAnalysisDto>();

        foreach (var eq in examQuestions)
        {
            var q = eq.Question;
            var qAnswers = answerLookup[q.Id].ToList();
            int total = qAnswers.Count;
            int correct = qAnswers.Count(a => a.IsCorrect);
            int empty = qAnswers.Count(a => a.SelectedOption == OptionLetter.Empty);
            int wrong = total - correct - empty;

            double p = total > 0 ? (double)correct / total : 0;

            // Üst/alt grup doğru sayıları
            int upperCorrect = qAnswers.Count(a => a.IsCorrect && upperGroup.Contains(a.StudentId));
            int lowerCorrect = qAnswers.Count(a => a.IsCorrect && lowerGroup.Contains(a.StudentId));

            // D = (Pü − Pa) / Ng
            double pUpper = (double)upperCorrect / groupSize;
            double pLower = (double)lowerCorrect / groupSize;
            double d = pUpper - pLower;

            // Madde güvenilirlik indeksi
            double reliability = d * Math.Sqrt(p * (1 - p));

            // Çeldirici detayları — k şıklı bir soruda ideal: (1 − p) / (k − 1)
            var distractors = BuildDistractors(q, qAnswers, p);
            double distractorEffectiveness = ComputeDistractorEffectiveness(distractors);

            results.Add(new ItemAnalysisDto
            {
                QuestionId = q.Id,
                QuestionNumber = eq.OrderInExam,
                QuestionText = q.QuestionText,
                CorrectOption = q.CorrectOption.ToString(),
                TotalStudents = total,
                CorrectCount = correct,
                WrongCount = wrong,
                EmptyCount = empty,

                DifficultyIndex = Math.Round(p, 3),
                DifficultyCategory = ClassifyDifficulty(p),

                DiscriminationIndex = Math.Round(d, 3),
                DiscriminationCategory = ClassifyDiscrimination(d),
                UpperGroupCorrect = upperCorrect,
                LowerGroupCorrect = lowerCorrect,
                GroupSize = groupSize,

                ItemReliabilityIndex = Math.Round(reliability, 3),
                DistractorEffectivenessIndex = Math.Round(distractorEffectiveness, 3),
                Distractors = distractors
            });
        }

        return results;
    }

    private static List<DistractorDetailDto> BuildDistractors(
        Core.Entities.Question q,
        List<Core.Entities.StudentAnswer> answers,
        double p)
    {
        var options = new[] { OptionLetter.A, OptionLetter.B, OptionLetter.C, OptionLetter.D, OptionLetter.E };
        var available = options.Where(opt => HasOption(q, opt)).ToList();
        int k = available.Count;
        int total = answers.Count;

        // İdeal çeldirici oranı = (1 − p) / (k − 1)
        double idealRate = k > 1 ? (1 - p) / (k - 1) * 100 : 0;

        var list = new List<DistractorDetailDto>();
        foreach (var opt in available)
        {
            int cnt = answers.Count(a => a.SelectedOption == opt);
            double rate = total > 0 ? (double)cnt / total * 100 : 0;
            bool isCorrect = opt == q.CorrectOption;

            list.Add(new DistractorDetailDto
            {
                Option = opt.ToString(),
                SelectedCount = cnt,
                SelectionRate = Math.Round(rate, 1),
                IsCorrectOption = isCorrect,
                IdealRate = Math.Round(idealRate, 1),
                // En az %5 öğrencinin seçtiği çeldirici "etkili" sayılır
                IsEffective = !isCorrect && rate >= 5.0
            });
        }
        return list;
    }

    private static bool HasOption(Core.Entities.Question q, OptionLetter opt) => opt switch
    {
        OptionLetter.A => !string.IsNullOrWhiteSpace(q.OptionA),
        OptionLetter.B => !string.IsNullOrWhiteSpace(q.OptionB),
        OptionLetter.C => !string.IsNullOrWhiteSpace(q.OptionC),
        OptionLetter.D => !string.IsNullOrWhiteSpace(q.OptionD),
        OptionLetter.E => !string.IsNullOrWhiteSpace(q.OptionE),
        _ => false
    };

    /// <summary>
    /// Doğru şık dışındaki çeldiricilerin etkinlik oranı.
    /// Etkili çeldirici / toplam çeldirici sayısı.
    /// </summary>
    private static double ComputeDistractorEffectiveness(List<DistractorDetailDto> distractors)
    {
        var nonCorrect = distractors.Where(d => !d.IsCorrectOption).ToList();
        if (nonCorrect.Count == 0) return 0;
        return (double)nonCorrect.Count(d => d.IsEffective) / nonCorrect.Count;
    }

    /// <summary>
    /// Klasik (Crocker & Algina, 1986) zorluk kategorileri:
    ///   p &lt; 0.20 → Çok Zor, &lt; 0.40 → Zor, &lt; 0.60 → Orta,
    ///   &lt; 0.80 → Kolay, ≥ 0.80 → Çok Kolay.
    /// </summary>
    private static string ClassifyDifficulty(double p) => p switch
    {
        < 0.20 => "Çok Zor",
        < 0.40 => "Zor",
        < 0.60 => "Orta",
        < 0.80 => "Kolay",
        _ => "Çok Kolay"
    };

    /// <summary>
    /// Ebel (1979) ayırt edicilik kategorileri:
    ///   D &lt; 0.20 → Zayıf (atılmalı),
    ///   0.20 ≤ D &lt; 0.30 → Düzeltilmeli,
    ///   0.30 ≤ D &lt; 0.40 → Kabul edilebilir,
    ///   D ≥ 0.40 → Çok iyi.
    /// </summary>
    private static string ClassifyDiscrimination(double d) => d switch
    {
        < 0.20 => "Zayıf (Atılmalı)",
        < 0.30 => "Düzeltilmeli",
        < 0.40 => "Kabul Edilebilir",
        _ => "Çok İyi"
    };

    private List<ItemAnalysisDto> BuildBasicAnalysis(
        List<Core.Entities.ExamQuestion> examQuestions,
        List<Core.Entities.StudentAnswer> answers)
    {
        var lookup = answers.ToLookup(a => a.QuestionId);
        var results = new List<ItemAnalysisDto>();

        foreach (var eq in examQuestions)
        {
            var q = eq.Question;
            var qAnswers = lookup[q.Id].ToList();
            int total = qAnswers.Count;
            int correct = qAnswers.Count(a => a.IsCorrect);
            int empty = qAnswers.Count(a => a.SelectedOption == OptionLetter.Empty);
            int wrong = total - correct - empty;
            double p = total > 0 ? (double)correct / total : 0;
            var distractors = BuildDistractors(q, qAnswers, p);

            results.Add(new ItemAnalysisDto
            {
                QuestionId = q.Id,
                QuestionNumber = eq.OrderInExam,
                QuestionText = q.QuestionText,
                CorrectOption = q.CorrectOption.ToString(),
                TotalStudents = total,
                CorrectCount = correct,
                WrongCount = wrong,
                EmptyCount = empty,
                DifficultyIndex = Math.Round(p, 3),
                DifficultyCategory = ClassifyDifficulty(p),
                DiscriminationIndex = 0,
                DiscriminationCategory = "Veri Yetersiz",
                Distractors = distractors,
                DistractorEffectivenessIndex = Math.Round(ComputeDistractorEffectiveness(distractors), 3)
            });
        }
        return results;
    }
}
