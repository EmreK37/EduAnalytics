using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.UI.Services;
using EduAnalytics.UI.Services.AIAssistant;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace EduAnalytics.UI.ViewModels;

public partial class ExamAnalysisViewModel : ObservableObject, IAIContextProvider
{
    private readonly IExamAnalysisService _examService;
    private readonly IDistractorAnalysisService _distractorService;
    private readonly IStudentPerformanceService _studentService;
    private readonly ILearningOutcomePerformanceService _learningOutcomeService;
    private readonly IExamCancellationService _cancellationService;
    private readonly IExamBalanceCheckService _balanceService;
    private readonly IExamStatisticsService _statisticsService;
    private readonly IItemAnalysisService _itemAnalysisService;
    private readonly IThemeService _themeService;

    /// <summary>Parent (MainViewModel) bu event'i dinleyerek Cevap Girişi ekranına geçer.</summary>
    public event Action<int>? OpenAnswerEntryRequested;

    private int _currentExamId;

    [ObservableProperty] private ExamSummaryDto? _summary;
    [ObservableProperty] private ObservableCollection<LearningOutcomePerformanceDto> _learningOutcomes = new();
    [ObservableProperty] private ObservableCollection<QuestionAnalysisDto> _questions = new();
    [ObservableProperty] private ObservableCollection<QuestionAnalysisDto> _strongDistractors = new();
    [ObservableProperty] private ObservableCollection<StudentPerformanceDto> _students = new();
    [ObservableProperty] private ObservableCollection<AnalysisInsightVm> _executiveInsights = new();
    [ObservableProperty] private ObservableCollection<string> _aiQuickPrompts = new();

    // Öğrenci Filtreleme
    [ObservableProperty] private ObservableCollection<string> _availableClasses = new();
    [ObservableProperty] private string? _selectedClass;
    [ObservableProperty] private ObservableCollection<StudentPerformanceDto> _filteredStudents = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private int _selectedTabIndex;

    // Grafikler — Build* metotları ExamAnalysisViewModel.Charts.cs içinde
    [ObservableProperty] private ISeries[] _learningOutcomeChartSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _learningOutcomeChartXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _learningOutcomeChartYAxes = Array.Empty<Axis>();

    [ObservableProperty] private ISeries[] _questionSuccessSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _questionXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _questionYAxes = Array.Empty<Axis>();
    [ObservableProperty] private ISeries[] _summaryQuestionSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _summaryQuestionXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _summaryQuestionYAxes = Array.Empty<Axis>();
    [ObservableProperty] private string _summaryQuestionMetric = "Success";

    public bool IsSummaryQuestionSuccessSelected => SummaryQuestionMetric == "Success";
    public bool IsSummaryQuestionDifficultySelected => SummaryQuestionMetric == "Difficulty";
    public bool IsSummaryQuestionDiscriminationSelected => SummaryQuestionMetric == "Discrimination";

    [ObservableProperty] private ISeries[] _scoreDistributionSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _scoreDistributionXAxes = Array.Empty<Axis>();

    [ObservableProperty] private ISeries[] _successRateDoughnutSeries = Array.Empty<ISeries>();
    [ObservableProperty] private int _correctAnswerCount;
    [ObservableProperty] private int _wrongAnswerCount;
    [ObservableProperty] private int _emptyAnswerCount;
    [ObservableProperty] private int _totalAnswerCount;

    public double CorrectAnswerRate => TotalAnswerCount == 0 ? 0 : CorrectAnswerCount * 100.0 / TotalAnswerCount;
    public double WrongAnswerRate => TotalAnswerCount == 0 ? 0 : WrongAnswerCount * 100.0 / TotalAnswerCount;
    public double EmptyAnswerRate => TotalAnswerCount == 0 ? 0 : EmptyAnswerCount * 100.0 / TotalAnswerCount;

    [ObservableProperty] private ISeries[] _radarSeries = Array.Empty<ISeries>();
    [ObservableProperty] private PolarAxis[] _radarAxes = Array.Empty<PolarAxis>();

    [ObservableProperty] private ExamBalanceReportDto? _balanceReport;

    // ─── FAZ 6: İstatistik & Madde Analizi ───
    [ObservableProperty] private ExamStatisticsDto? _statistics;
    [ObservableProperty] private ObservableCollection<ItemAnalysisDto> _itemAnalysis = new();

    /// <summary>Kabul edilebilirlik indeksi (geçme eşiği). Varsayılan %50.</summary>
    [ObservableProperty] private double _acceptabilityIndex = 50.0;

    [ObservableProperty] private ISeries[] _difficultyChartSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _difficultyChartXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _difficultyChartYAxes = Array.Empty<Axis>();

    [ObservableProperty] private ISeries[] _discriminationChartSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _discriminationChartXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _discriminationChartYAxes = Array.Empty<Axis>();

    [ObservableProperty] private ISeries[] _upperLowerSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _upperLowerXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _upperLowerYAxes = Array.Empty<Axis>();

    [ObservableProperty] private ISeries[] _answerDistributionSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _answerDistributionXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _answerDistributionYAxes = Array.Empty<Axis>();

    public string ExamAnalysisSubtitle => Summary == null
        ? string.Empty
        : $"{Summary.CourseName} · {Summary.ExamDate:dd.MM.yyyy}";

    public ExamAnalysisViewModel(
        IExamAnalysisService examService,
        IDistractorAnalysisService distractorService,
        IStudentPerformanceService studentService,
        ILearningOutcomePerformanceService learningOutcomeService,
        IExamCancellationService cancellationService,
        IExamBalanceCheckService balanceService,
        IExamStatisticsService statisticsService,
        IItemAnalysisService itemAnalysisService,
        IThemeService themeService)
    {
        _examService = examService;
        _distractorService = distractorService;
        _studentService = studentService;
        _learningOutcomeService = learningOutcomeService;
        _cancellationService = cancellationService;
        _balanceService = balanceService;
        _statisticsService = statisticsService;
        _itemAnalysisService = itemAnalysisService;
        _themeService = themeService;
        _themeService.ThemeChanged += () =>
        {
            if (_currentExamId > 0)
                System.Windows.Application.Current.Dispatcher.Invoke(RebuildCharts);
        };
    }

    private void RebuildCharts()
    {
        var outcomes = LearningOutcomes.ToList();
        var questions = Questions.ToList();
        var students  = Students.ToList();
        var items     = ItemAnalysis.ToList();
        BuildLearningOutcomeChart(outcomes);
        BuildQuestionChart(questions);
        BuildScoreDistributionHistogram(students, Summary?.MaxPossibleScore ?? 0);
        BuildSuccessRateDoughnut(Summary?.AverageSuccessRate ?? 0, items);
        BuildRadarChart(outcomes);
        BuildDifficultyChart(items);
        BuildDiscriminationChart(items);
        BuildUpperLowerChart(items);
        BuildAnswerDistributionChart(items);
        UpdateSummaryQuestionChart();
    }

    public async Task LoadAsync(int examId)
    {
        _currentExamId = examId;
        IsLoading = true;
        ErrorMessage = null;

        Summary = null;
        BalanceReport = null;
        ExecutiveInsights.Clear();
        AiQuickPrompts.Clear();

        try
        {
            Summary = await _examService.GetSummaryAsync(examId);

            var learningOutcomeList = await _learningOutcomeService.AnalyzeExamAsync(examId);
            LearningOutcomes = new ObservableCollection<LearningOutcomePerformanceDto>(learningOutcomeList);

            var questionList = await _distractorService.AnalyzeExamAsync(examId);
            Questions = new ObservableCollection<QuestionAnalysisDto>(questionList);

            var strongList = await _distractorService.GetStrongDistractorsAsync(examId, 50);
            StrongDistractors = new ObservableCollection<QuestionAnalysisDto>(strongList);

            var studentList = await _studentService.GetExamRankingAsync(examId);
            Students = new ObservableCollection<StudentPerformanceDto>(studentList);

            BalanceReport = await _balanceService.AnalyzeAsync(examId);

            Statistics = await _statisticsService.ComputeAsync(examId, AcceptabilityIndex);

            var itemList = await _itemAnalysisService.AnalyzeAsync(examId);
            ItemAnalysis = new ObservableCollection<ItemAnalysisDto>(itemList);
            EnrichSummaryMetrics(learningOutcomeList, itemList);

            // Sınıf filtresini doldur
            var classes = Students.Select(s => s.ClassName).Where(c => !string.IsNullOrEmpty(c)).Distinct().OrderBy(c => c).ToList();
            var classList = new List<string> { "Tümü" };
            classList.AddRange(classes);
            AvailableClasses = new ObservableCollection<string>(classList);
            SelectedClass = "Tümü";

            BuildLearningOutcomeChart(learningOutcomeList);
            BuildQuestionChart(questionList);
            BuildScoreDistributionHistogram(studentList, Summary?.MaxPossibleScore ?? 0);
            BuildSuccessRateDoughnut(Summary?.AverageSuccessRate ?? 0, itemList);
            BuildRadarChart(learningOutcomeList);
            BuildDifficultyChart(itemList);
            BuildDiscriminationChart(itemList);
            BuildUpperLowerChart(itemList);
            BuildAnswerDistributionChart(itemList);
            UpdateSummaryQuestionChart();
            BuildExecutiveWorkspace();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"HATA: {ex.GetType().Name}\n{ex.Message}" +
                           (ex.InnerException != null ? $"\n\nİç Hata: {ex.InnerException.Message}" : "");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadAsync(int examId, int selectedTabIndex)
    {
        SelectedTabIndex = selectedTabIndex;
        await LoadAsync(examId);
        SelectedTabIndex = selectedTabIndex;
    }

    [RelayCommand]
    private async Task CancelQuestionAsync(QuestionAnalysisDto? q)
    {
        if (q == null || _currentExamId <= 0) return;

        var dialog = new EduAnalytics.UI.Views.QuestionCancelDialog
        {
            QuestionInfo = $"Soru {q.QuestionNumber}: {q.QuestionText[..Math.Min(q.QuestionText.Length, 100)]}",
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            await _cancellationService.CancelQuestionAsync(_currentExamId, q.QuestionId, dialog.Reason);
            await LoadAsync(_currentExamId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"İptal hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenAnswerEntry()
    {
        if (_currentExamId > 0)
            OpenAnswerEntryRequested?.Invoke(_currentExamId);
    }

    partial void OnAcceptabilityIndexChanged(double value)
    {
        if (_currentExamId > 0 && !IsLoading)
            _ = ReloadStatisticsAsync();
    }

    partial void OnSummaryChanged(ExamSummaryDto? value)
    {
        OnPropertyChanged(nameof(ExamAnalysisSubtitle));
    }

    partial void OnSummaryQuestionMetricChanged(string value)
    {
        OnPropertyChanged(nameof(IsSummaryQuestionSuccessSelected));
        OnPropertyChanged(nameof(IsSummaryQuestionDifficultySelected));
        OnPropertyChanged(nameof(IsSummaryQuestionDiscriminationSelected));
        UpdateSummaryQuestionChart();
    }

    [RelayCommand]
    private void SelectSummaryQuestionMetric(string? metric)
    {
        if (string.IsNullOrWhiteSpace(metric))
            return;

        SummaryQuestionMetric = metric;
    }

    private void UpdateSummaryQuestionChart()
    {
        SummaryQuestionXAxes = SummaryQuestionMetric switch
        {
            "Difficulty" => DifficultyChartXAxes,
            "Discrimination" => DiscriminationChartXAxes,
            _ => QuestionXAxes
        };

        SummaryQuestionYAxes = SummaryQuestionMetric switch
        {
            "Difficulty" => DifficultyChartYAxes,
            "Discrimination" => DiscriminationChartYAxes,
            _ => QuestionYAxes
        };

        SummaryQuestionSeries = SummaryQuestionMetric switch
        {
            "Difficulty" => DifficultyChartSeries,
            "Discrimination" => DiscriminationChartSeries,
            _ => QuestionSuccessSeries
        };
    }

    private async Task ReloadStatisticsAsync()
    {
        try
        {
            Statistics = await _statisticsService.ComputeAsync(_currentExamId, AcceptabilityIndex);
            EnrichSummaryMetrics(LearningOutcomes.ToList(), ItemAnalysis.ToList());
            BuildExecutiveWorkspace();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"İstatistik güncellenemedi: {ex.Message}";
        }
    }

    private void EnrichSummaryMetrics(
        IReadOnlyCollection<LearningOutcomePerformanceDto> outcomes,
        IReadOnlyCollection<ItemAnalysisDto> items)
    {
        if (Summary == null)
            return;

        if (Statistics != null)
        {
            Summary.MedianScore = Statistics.Median;
            Summary.CronbachAlpha = Statistics.CronbachAlpha;
            Summary.CronbachAlphaInterpretation = Statistics.CronbachAlphaInterpretation;
            Summary.PassingScore = Statistics.PassingScore;
            Summary.PassRate = Statistics.PassRate;
        }

        if (items.Count > 0)
        {
            Summary.AverageDifficultyIndex = Math.Round(items.Average(i => i.DifficultyIndex), 3);
            Summary.AverageDiscriminationIndex = Math.Round(items.Average(i => i.DiscriminationIndex), 3);
        }

        var lowestOutcome = outcomes.OrderBy(o => o.SuccessRate).FirstOrDefault();
        if (lowestOutcome != null)
        {
            Summary.LowestLearningOutcomeName = lowestOutcome.OutcomeName;
            Summary.LowestLearningOutcomeSuccessRate = Math.Round(lowestOutcome.SuccessRate, 1);
        }

        OnPropertyChanged(nameof(Summary));
    }

    private void BuildExecutiveWorkspace()
    {
        var alpha = Statistics?.CronbachAlpha ?? Summary?.CronbachAlpha ?? 0;
        var lowDiscriminationCount = ItemAnalysis.Count(i => i.DiscriminationIndex < 0.20);
        var extremeDifficultyCount = ItemAnalysis.Count(i => i.DifficultyIndex < 0.25 || i.DifficultyIndex > 0.90);
        var weakOutcomeCount = LearningOutcomes.Count(o => o.SuccessRate < 60);
        var balanceWarnings = BalanceReport?.Warnings.Count ?? 0;

        ExecutiveInsights = new ObservableCollection<AnalysisInsightVm>
        {
            new()
            {
                Title = "Güvenilirlik",
                Value = alpha <= 0 ? "—" : alpha.ToString("0.000"),
                Detail = alpha switch
                {
                    <= 0 => "İç tutarlılık hesaplanamadı.",
                    < 0.60 => "Düşük. Maddeler aynı yapıyı tutarlı ölçmüyor olabilir.",
                    < 0.70 => "Sınırda. Sorunlu madde ve kapsam dağılımı incelenmeli.",
                    < 0.80 => "Kabul edilebilir. Revizyonla güçlenebilir.",
                    _ => "Güçlü. Sınav iç tutarlılığı iyi görünüyor."
                },
                Severity = alpha switch
                {
                    <= 0 => "Info",
                    < 0.60 => "Critical",
                    < 0.70 => "Warning",
                    _ => "Success"
                }
            },
            new()
            {
                Title = "Madde Riski",
                Value = (lowDiscriminationCount + extremeDifficultyCount).ToString(),
                Detail = $"{lowDiscriminationCount} düşük ayırt edicilik, {extremeDifficultyCount} aşırı kolay/zor madde adayı.",
                Severity = lowDiscriminationCount + extremeDifficultyCount switch
                {
                    0 => "Success",
                    <= 2 => "Warning",
                    _ => "Critical"
                }
            },
            new()
            {
                Title = "Kapsam & ÖÇ",
                Value = weakOutcomeCount.ToString(),
                Detail = weakOutcomeCount == 0
                    ? "ÖÇ performansları kritik eşik altında görünmüyor."
                    : $"{weakOutcomeCount} öğrenim çıktısı %60 altında. Denge uyarısı: {balanceWarnings}.",
                Severity = weakOutcomeCount switch
                {
                    0 => "Success",
                    <= 2 => "Warning",
                    _ => "Critical"
                }
            }
        };

        AiQuickPrompts = new ObservableCollection<string>
        {
            "Bu sınavın güvenilirliği neden düştü? Cronbach alfa, madde güçlüğü ve ayırt edicilik verilerine göre açıkla.",
            "Hangi soruları revize etmeliyim? Madde analizi verilerine göre öncelikli aksiyon listesi çıkar.",
            "En zayıf öğrenim çıktıları için ders içi iyileştirme önerileri ver.",
            "Bu sınav için akademik kurul raporuna uygun kısa bir değerlendirme paragrafı yaz."
        };
    }

    partial void OnSelectedClassChanged(string? value) => FilterStudents();

    private void FilterStudents()
    {
        if (string.IsNullOrEmpty(SelectedClass) || SelectedClass == "Tümü")
        {
            FilteredStudents = new ObservableCollection<StudentPerformanceDto>(Students);
        }
        else
        {
            var filtered = Students.Where(s => s.ClassName == SelectedClass).ToList();
            FilteredStudents = new ObservableCollection<StudentPerformanceDto>(filtered);
        }
    }

    public string? GetAIContext()
    {
        if (Summary == null) return null;

        var lines = new List<string>
        {
            $"Ekran: Sınav Analizi",
            $"Sınav: {Summary.ExamTitle}",
            $"Ders: {Summary.CourseName}",
            $"Tarih: {Summary.ExamDate:dd.MM.yyyy}",
            $"Öğrenci sayısı: {Summary.TotalStudents}",
            $"Soru sayısı: {Summary.TotalQuestions} (test: {Summary.MultipleChoiceCount}, klasik: {Summary.OpenEndedCount})",
            $"Maksimum puan: {Summary.MaxPossibleScore:0.##}",
            $"Ortalama puan: {Summary.AverageScore:0.##}",
            $"Ortalama başarı: %{Summary.AverageSuccessRate:0.##}",
            $"Cronbach alfa: {Summary.CronbachAlpha:0.###} ({Summary.CronbachAlphaInterpretation})",
            $"Ortalama madde güçlüğü: {Summary.AverageDifficultyIndex:0.###}",
            $"Ortalama ayırt edicilik: {Summary.AverageDiscriminationIndex:0.###}",
            $"En düşük ÖÇ: {Summary.LowestLearningOutcomeName} (%{Summary.LowestLearningOutcomeSuccessRate:0.##})"
        };

        if (Statistics != null)
        {
            lines.Add($"Standart sapma: {Statistics.StandardDeviation:0.###}");
            lines.Add($"Çarpıklık: {Statistics.Skewness:0.###}");
            lines.Add($"Geçme puanı: {Statistics.PassingScore:0.##}");
            lines.Add($"Geçme oranı: %{Statistics.PassRate:0.##}");
        }

        var weakOutcomes = LearningOutcomes
            .OrderBy(o => o.SuccessRate)
            .Take(3)
            .Select(o => $"{o.OutcomeName}: %{o.SuccessRate:0.##} ({o.PerformanceLevel}, soru: {o.RelatedQuestionCount})")
            .ToList();
        if (weakOutcomes.Count > 0)
            lines.Add("En zayıf ÖÇ'ler: " + string.Join("; ", weakOutcomes));

        var problematicItems = ItemAnalysis
            .Where(i => i.DiscriminationIndex < 0.20 || i.DifficultyIndex < 0.25 || i.DifficultyIndex > 0.90)
            .OrderBy(i => i.DiscriminationIndex)
            .ThenBy(i => Math.Abs(0.55 - i.DifficultyIndex))
            .Take(5)
            .Select(i => $"Soru {i.QuestionNumber}: p={i.DifficultyIndex:0.###} ({i.DifficultyCategory}), D={i.DiscriminationIndex:0.###} ({i.DiscriminationCategory}), çeldirici={i.DistractorEffectivenessIndex:0.###}")
            .ToList();
        if (problematicItems.Count > 0)
            lines.Add("Sorunlu madde adayları: " + string.Join("; ", problematicItems));

        if (BalanceReport != null)
        {
            lines.Add($"Sınav dengelilik skoru: {BalanceReport.BalanceScore:0.##}/100");
            lines.Add($"Dağılım eşitsizliği: {BalanceReport.DistributionInequality:0.###}");

            var warnings = BalanceReport.Warnings
                .Take(4)
                .Select(w => $"{w.Severity}: {w.Message}")
                .ToList();
            if (warnings.Count > 0)
                lines.Add("Denge uyarıları: " + string.Join("; ", warnings));
        }

        return string.Join(Environment.NewLine, lines);
    }
}

public sealed class AnalysisInsightVm
{
    public string Title { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string Severity { get; init; } = "Info";
}
