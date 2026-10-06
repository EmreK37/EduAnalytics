using EduAnalytics.Business.Services.Implementations;
using EduAnalytics.Core.Enums;
using EduAnalytics.DataAccess.Context;
using EduAnalytics.DataAccess.Seed;
using Microsoft.EntityFrameworkCore;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("════════════════════════════════════════════════════════════════════");
Console.WriteLine("  EduAnalytics — Seed + Analiz Demo");
Console.WriteLine("════════════════════════════════════════════════════════════════════");
Console.WriteLine();

try
{
    var factory = new DesignTimeDbContextFactory();
    using var context = factory.CreateDbContext(Array.Empty<string>());

    // ─── 1. SEED ─────────────────────────────────────────────────────
    Console.WriteLine("→ Veritabanı hazırlanıyor ve seed uygulanıyor...");
    DbInitializer.Seed(context);
    Console.WriteLine("  ✓ Tamam. Kayıt sayıları:");
    Console.WriteLine($"     Kullanıcı: {context.Users.Count()}  Ders: {context.Courses.Count()}  " +
                      $"Konu: {context.Topics.Count()}  Sınav: {context.Exams.Count()}");
    Console.WriteLine($"     Soru: {context.Questions.Count()}  Öğrenci: {context.Students.Count()}  " +
                      $"Cevap: {context.StudentAnswers.Count()}");
    Console.WriteLine();

    // ─── 2. SERVİSLERİ KUR ─────────────────────────────────────────
    var examAnalysis = new ExamAnalysisService(context);
    var distractor = new DistractorAnalysisService(context);
    var topicPerf = new TopicPerformanceService(context);
    var studentPerf = new StudentPerformanceService(context);

    var examId = context.Exams.First().Id;

    // ─── 3. SINAV ÖZETİ ─────────────────────────────────────────────
    Console.WriteLine("════════════════════════════════════════════════════════════════════");
    Console.WriteLine("  1) SINAV ÖZETİ");
    Console.WriteLine("════════════════════════════════════════════════════════════════════");
    var summary = await examAnalysis.GetSummaryAsync(examId);
    if (summary != null)
    {
        Console.WriteLine($"  Sınav          : {summary.ExamTitle}");
        Console.WriteLine($"  Ders           : {summary.CourseName}");
        Console.WriteLine($"  Tarih          : {summary.ExamDate:dd.MM.yyyy}");
        Console.WriteLine($"  Öğrenci sayısı : {summary.TotalStudents}");
        Console.WriteLine($"  Soru sayısı    : {summary.TotalQuestions}");
        Console.WriteLine($"  Ortalama       : {summary.AverageScore}/{summary.TotalQuestions}  " +
                          $"({summary.AverageSuccessRate}%)");
        Console.WriteLine($"  En yüksek      : {summary.HighestScore} doğru");
        Console.WriteLine($"  En düşük       : {summary.LowestScore} doğru");
    }
    Console.WriteLine();

    // ─── 4. KONU BAZLI BA�?ARI ───────────────────────────────────────
    Console.WriteLine("════════════════════════════════════════════════════════════════════");
    Console.WriteLine("  2) KONU BAZLI BA�?ARI (Bologna Çıktıları)");
    Console.WriteLine("════════════════════════════════════════════════════════════════════");
    var topics = await topicPerf.AnalyzeExamAsync(examId);
    Console.WriteLine("  Hafta  Konu                              Soru  Başarı    Seviye");
    Console.WriteLine("  ─────  ────────────────────────────────  ────  ───────   ────────");
    foreach (var t in topics)
    {
        Console.WriteLine($"  {t.WeekNumber,-5}  {Truncate(t.TopicTitle, 32),-32}  " +
                          $"{t.RelatedQuestionCount,4}  {t.SuccessRate,6:0.0}%   {t.PerformanceLevel}");
    }
    Console.WriteLine();

    var weakTopics = topics.Where(t => t.SuccessRate < 60).ToList();
    if (weakTopics.Any())
    {
        Console.WriteLine("  ⚠ DİKKAT — Zayıf konular (<%60):");
        foreach (var t in weakTopics)
            Console.WriteLine($"     • {t.TopicTitle} ({t.SuccessRate}%)");
        Console.WriteLine();
    }

    // ─── 5. ÇELDİRİCİ ANALİZİ (Projenin Kalbi) ─────────────────────
    Console.WriteLine("════════════════════════════════════════════════════════════════════");
    Console.WriteLine("  3) ÇELDİRİCİ ANALİZİ  (Yanlış yapanlar hangi şıkkı seçti?)");
    Console.WriteLine("════════════════════════════════════════════════════════════════════");
    var questionAnalyses = await distractor.AnalyzeExamAsync(examId);
    Console.WriteLine("  Soru  Doğru  Başarı    Dağılım (A/B/C/D)       Güçlü Çeldirici");
    Console.WriteLine("  ────  ─────  ───────   ─────────────────       ────────────────────");
    foreach (var q in questionAnalyses)
    {
        var dist = $"{q.OptionACount,2}/{q.OptionBCount,2}/{q.OptionCCount,2}/{q.OptionDCount,2}";
        var distrStr = q.StrongestDistractorOption != null
            ? $"{q.StrongestDistractorOption} ({q.StrongestDistractorCount} kişi, %{q.StrongestDistractorRate})"
            : "—";
        Console.WriteLine($"  Q{q.QuestionNumber,-3}  {q.CorrectOption,-5}  {q.SuccessRate,6:0.0}%   " +
                          $"{dist,-22}  {distrStr}");
    }
    Console.WriteLine();

    var strongDistractors = await distractor.GetStrongDistractorsAsync(examId, 50);
    Console.WriteLine("  ⚠ GÜÇLÜ ÇELDİRİCİLER (yanlış yapanların ≥%50'si aynı şıkkı seçti):");
    foreach (var q in strongDistractors)
    {
        Console.WriteLine($"     • Soru {q.QuestionNumber}: '{q.StrongestDistractorOption}' şıkkı — " +
                          $"yanlış yapan {q.WrongCount} kişiden {q.StrongestDistractorCount}'i " +
                          $"(%{q.StrongestDistractorRate}) bu şıkkı seçti");
    }
    Console.WriteLine();

    // ─── 6. Ö�?RENCİ SIRALAMASI ─────────────────────────────────────
    Console.WriteLine("════════════════════════════════════════════════════════════════════");
    Console.WriteLine("  4) Ö�?RENCİ SIRALAMASI (İlk 10)");
    Console.WriteLine("════════════════════════════════════════════════════════════════════");
    var ranking = await studentPerf.GetExamRankingAsync(examId);
    Console.WriteLine("  Sıra  Numara    Öğrenci                   Doğru  Başarı  Zayıf Konular");
    Console.WriteLine("  ────  ────────  ────────────────────────  ─────  ──────  ─────────────");
    foreach (var s in ranking.Take(10))
    {
        var weak = s.WeakTopics.Any() ? string.Join(", ", s.WeakTopics) : "—";
        Console.WriteLine($"  {s.ClassRank,4}  {s.StudentNumber,-8}  {Truncate(s.FullName, 24),-24}  " +
                          $"{s.CorrectAnswers,5}  {s.SuccessRate,5:0.0}%  {Truncate(weak, 40)}");
    }
    Console.WriteLine();

    Console.WriteLine("  Son 3 öğrenci (en düşük başarı):");
    foreach (var s in ranking.TakeLast(3))
    {
        Console.WriteLine($"     • {s.ClassRank}. {s.FullName} — {s.CorrectAnswers} doğru " +
                          $"(%{s.SuccessRate})");
    }
    Console.WriteLine();

    Console.WriteLine("════════════════════════════════════════════════════════════════════");
    Console.WriteLine("  ✓ Tüm analizler başarıyla çalıştı. Çıkmak için Enter'a bas.");
    Console.WriteLine("════════════════════════════════════════════════════════════════════");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("✗ HATA OLU�?TU:");
    Console.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException != null)
        Console.WriteLine($"  İç Hata: {ex.InnerException.Message}");
    Console.WriteLine();
    Console.WriteLine("Çıkmak için Enter'a bas.");
}

Console.ReadLine();

// Helper
static string Truncate(string value, int maxLen)
    => string.IsNullOrEmpty(value) ? "" : (value.Length <= maxLen ? value : value[..(maxLen - 1)] + "…");
