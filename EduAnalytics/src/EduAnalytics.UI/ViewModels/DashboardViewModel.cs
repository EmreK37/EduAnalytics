using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.UI.Services;

namespace EduAnalytics.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IExamAnalysisService _examAnalysis;
    private readonly IExamStatisticsService _statisticsService;
    private readonly IItemAnalysisService _itemAnalysisService;
    private readonly ILearningOutcomePerformanceService _learningOutcomeService;
    private readonly IStudentService _studentService;
    private readonly IUserProfileService _userProfile;

    public event Action<int>? OpenAnalysisRequested;
    public event Action<DateTime?>? CalendarRequested;
    public event Action? ExamManagementRequested;
    public event Action? StudentsRequested;
    public event Action<int>? LatestExamStatisticsRequested;

    private List<ExamSummaryDto> _allExams = new();

    [ObservableProperty]
    private ObservableCollection<ExamSummaryDto> _exams = new();

    [ObservableProperty]
    private ObservableCollection<ExamSummaryDto> _filteredExams = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _studentCount;

    [ObservableProperty]
    private double _overallAverageSuccessRate;

    [ObservableProperty]
    private string _lastActivityDate = "—";

    [ObservableProperty]
    private DateTime? _lastActivityExamDate;

    [ObservableProperty]
    private int? _lastActivityExamId;

    public string DisplayUserName =>
        string.IsNullOrWhiteSpace(_userProfile.Current.Username)
            ? "Edu"
            : _userProfile.Current.Username;

    public string GreetingText
    {
        get
        {
            var hour = DateTime.Now.Hour;
            return hour switch
            {
                >= 5 and < 12 => "Günaydın, ",
                >= 12 and < 17 => "Tünaydın, ",
                >= 17 and < 22 => "İyi akşamlar, ",
                _ => "İyi geceler, "
            };
        }
    }

    public DashboardViewModel(
        IExamAnalysisService examAnalysis,
        IExamStatisticsService statisticsService,
        IItemAnalysisService itemAnalysisService,
        ILearningOutcomePerformanceService learningOutcomeService,
        IStudentService studentService,
        IUserProfileService userProfile)
    {
        _examAnalysis = examAnalysis;
        _statisticsService = statisticsService;
        _itemAnalysisService = itemAnalysisService;
        _learningOutcomeService = learningOutcomeService;
        _studentService = studentService;
        _userProfile = userProfile;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        OnPropertyChanged(nameof(DisplayUserName));
        OnPropertyChanged(nameof(GreetingText));
        try
        {
            var listTask    = _examAnalysis.GetAllExamSummariesAsync();
            var studentsTask = _studentService.GetAllAsync();

            var list = await listTask;
            foreach (var exam in list)
                await EnrichDashboardMetricsAsync(exam);

            _allExams = list.ToList();
            Exams = new ObservableCollection<ExamSummaryDto>(_allExams);
            ApplyExamFilter();

            StudentCount = (await studentsTask).Count;

            OverallAverageSuccessRate = Exams.Count > 0
                ? Math.Round(Exams.Average(e => e.AverageSuccessRate), 1)
                : 0;

            var latest = Exams.MaxBy(e => e.ExamDate);
            LastActivityExamDate = latest?.ExamDate.Date;
            LastActivityExamId = latest?.ExamId;
            LastActivityDate = latest is not null
                ? latest.ExamDate.ToString("dd MMM yyyy")
                : "—";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyExamFilter();

    private void ApplyExamFilter()
    {
        var query = (SearchText ?? string.Empty).Trim();
        IEnumerable<ExamSummaryDto> filtered = _allExams;

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = _allExams.Where(exam =>
                Contains(exam.ExamTitle, query) ||
                Contains(exam.CourseName, query) ||
                Contains(exam.ExamDate.ToString("dd MMM yyyy"), query) ||
                Contains(exam.ExamDate.ToString("dd.MM.yyyy"), query));
        }

        FilteredExams = new ObservableCollection<ExamSummaryDto>(filtered);
    }

    private static bool Contains(string? source, string query) =>
        !string.IsNullOrWhiteSpace(source) &&
        source.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    private async Task EnrichDashboardMetricsAsync(ExamSummaryDto exam)
    {
        var statistics = await _statisticsService.ComputeAsync(exam.ExamId, 50);
        if (statistics != null)
        {
            exam.MedianScore = statistics.Median;
            exam.CronbachAlpha = statistics.CronbachAlpha;
            exam.CronbachAlphaInterpretation = statistics.CronbachAlphaInterpretation;
            exam.PassingScore = statistics.PassingScore;
            exam.PassRate = statistics.PassRate;
        }

        var itemAnalysis = await _itemAnalysisService.AnalyzeAsync(exam.ExamId);
        if (itemAnalysis.Count > 0)
        {
            exam.AverageDifficultyIndex = Math.Round(itemAnalysis.Average(i => i.DifficultyIndex), 3);
            exam.AverageDiscriminationIndex = Math.Round(itemAnalysis.Average(i => i.DiscriminationIndex), 3);
        }

        var outcomes = await _learningOutcomeService.AnalyzeExamAsync(exam.ExamId);
        var lowestOutcome = outcomes.OrderBy(o => o.SuccessRate).FirstOrDefault();
        if (lowestOutcome != null)
        {
            exam.LowestLearningOutcomeName = lowestOutcome.OutcomeName;
            exam.LowestLearningOutcomeSuccessRate = Math.Round(lowestOutcome.SuccessRate, 1);
        }
    }

    [RelayCommand]
    private void OpenAnalysis(ExamSummaryDto? exam)
    {
        if (exam != null)
            OpenAnalysisRequested?.Invoke(exam.ExamId);
    }

    [RelayCommand]
    private void OpenCalendar()
    {
        CalendarRequested?.Invoke(LastActivityExamDate);
    }

    [RelayCommand]
    private void OpenExamManagement()
    {
        ExamManagementRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenLatestExamStatistics()
    {
        if (LastActivityExamId.HasValue)
            LatestExamStatisticsRequested?.Invoke(LastActivityExamId.Value);
        else
            ExamManagementRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenStudents()
    {
        StudentsRequested?.Invoke();
    }
}
