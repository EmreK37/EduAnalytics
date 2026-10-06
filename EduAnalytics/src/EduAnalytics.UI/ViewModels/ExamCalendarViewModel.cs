using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;

namespace EduAnalytics.UI.ViewModels;

public partial class ExamCalendarViewModel : ObservableObject
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private readonly IExamCrudService _examService;

    [ObservableProperty] private ObservableCollection<ExamListItemDto> _exams = new();
    [ObservableProperty] private ObservableCollection<CalendarDayViewModel> _calendarDays = new();
    [ObservableProperty] private ObservableCollection<ExamListItemDto> _selectedDayExams = new();
    [ObservableProperty] private DateTime _displayMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _selectedDate;
    [ObservableProperty] private bool _hasSelectedDayExams;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public event Action<int>? OpenExamRequested;

    public ExamCalendarViewModel(IExamCrudService examService)
    {
        _examService = examService;
    }

    public IReadOnlyList<string> WeekdayNames { get; } = ["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"];

    public string MonthTitle => DisplayMonth.ToString("MMMM yyyy", TurkishCulture);

    public string SelectedDateTitle => SelectedDate.HasValue
        ? SelectedDate.Value.ToString("d MMMM yyyy", TurkishCulture)
        : "Gün seçin";

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var list = await _examService.GetAllExamsAsync();
            Exams = new ObservableCollection<ExamListItemDto>(list);
            BuildCalendar(keepSelection: true);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Takvim yüklenemedi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadAsync(DateTime? focusDate)
    {
        if (focusDate.HasValue)
        {
            DisplayMonth = new DateTime(focusDate.Value.Year, focusDate.Value.Month, 1);
            SelectedDate = focusDate.Value.Date;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        DisplayMonth = DisplayMonth.AddMonths(-1);
        BuildCalendar(keepSelection: false);
    }

    [RelayCommand]
    private void NextMonth()
    {
        DisplayMonth = DisplayMonth.AddMonths(1);
        BuildCalendar(keepSelection: false);
    }

    [RelayCommand]
    private void GoToday()
    {
        DisplayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        SelectedDate = DateTime.Today;
        BuildCalendar(keepSelection: true);
    }

    [RelayCommand]
    private void SelectDay(CalendarDayViewModel? day)
    {
        if (day == null)
            return;

        if (!day.IsCurrentMonth)
            DisplayMonth = new DateTime(day.Date.Year, day.Date.Month, 1);

        SelectedDate = day.Date.Date;
        BuildCalendar(keepSelection: true);
    }

    [RelayCommand]
    private void OpenExam(ExamListItemDto? exam)
    {
        if (exam != null)
            OpenExamRequested?.Invoke(exam.Id);
    }

    partial void OnDisplayMonthChanged(DateTime value)
    {
        OnPropertyChanged(nameof(MonthTitle));
    }

    partial void OnSelectedDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(SelectedDateTitle));
    }

    private void BuildCalendar(bool keepSelection)
    {
        var monthStart = new DateTime(DisplayMonth.Year, DisplayMonth.Month, 1);
        var offset = ((int)monthStart.DayOfWeek + 6) % 7;
        var gridStart = monthStart.AddDays(-offset);
        var selected = ResolveSelectedDate(keepSelection);
        var days = new ObservableCollection<CalendarDayViewModel>();

        for (var i = 0; i < 42; i++)
        {
            var date = gridStart.AddDays(i);
            var dayExams = Exams
                .Where(e => e.ExamDate.Date == date.Date)
                .OrderBy(e => e.ExamDate)
                .ToList();

            days.Add(new CalendarDayViewModel(
                date,
                date.Month == DisplayMonth.Month && date.Year == DisplayMonth.Year,
                date.Date == DateTime.Today,
                selected.HasValue && date.Date == selected.Value.Date,
                dayExams));
        }

        CalendarDays = days;
        SelectedDate = selected;
        UpdateSelectedDayExams();
    }

    private DateTime? ResolveSelectedDate(bool keepSelection)
    {
        if (keepSelection &&
            SelectedDate.HasValue &&
            SelectedDate.Value.Year == DisplayMonth.Year &&
            SelectedDate.Value.Month == DisplayMonth.Month)
        {
            return SelectedDate.Value.Date;
        }

        if (DateTime.Today.Year == DisplayMonth.Year && DateTime.Today.Month == DisplayMonth.Month)
            return DateTime.Today;

        return Exams
            .Where(e => e.ExamDate.Year == DisplayMonth.Year && e.ExamDate.Month == DisplayMonth.Month)
            .OrderBy(e => e.ExamDate)
            .Select(e => (DateTime?)e.ExamDate.Date)
            .FirstOrDefault();
    }

    private void UpdateSelectedDayExams()
    {
        var list = SelectedDate.HasValue
            ? Exams
                .Where(e => e.ExamDate.Date == SelectedDate.Value.Date)
                .OrderBy(e => e.ExamDate)
                .ToList()
            : [];

        SelectedDayExams = new ObservableCollection<ExamListItemDto>(list);
        HasSelectedDayExams = SelectedDayExams.Count > 0;
    }
}

public partial class CalendarDayViewModel : ObservableObject
{
    public CalendarDayViewModel(
        DateTime date,
        bool isCurrentMonth,
        bool isToday,
        bool isSelected,
        IReadOnlyList<ExamListItemDto> exams)
    {
        Date = date;
        DayNumber = date.Day.ToString(CultureInfo.InvariantCulture);
        IsCurrentMonth = isCurrentMonth;
        IsToday = isToday;
        IsSelected = isSelected;
        Exams = new ObservableCollection<ExamListItemDto>(exams);
    }

    public DateTime Date { get; }
    public string DayNumber { get; }
    public bool IsCurrentMonth { get; }
    public bool IsToday { get; }
    public bool IsSelected { get; }
    public ObservableCollection<ExamListItemDto> Exams { get; }
    public bool HasExams => Exams.Count > 0;
    public bool HasMoreExams => Exams.Count > 2;
    public string OverflowText => $"+{Math.Max(0, Exams.Count - 2)}";
    public IEnumerable<ExamListItemDto> PreviewExams => Exams.Take(2);
}
