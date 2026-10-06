using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using EduAnalytics.Core.Enums;

namespace EduAnalytics.UI.ViewModels;

/// <summary>
/// Soru bankasından seçerek sınav oluşturma akışı.
/// Yeni soru üretmez; sadece havuzdan seçilen soruları sınava bağlar.
/// </summary>
public partial class ExamFromBankViewModel : ObservableObject
{
    private readonly IExamCrudService _examService;
    private readonly IQuestionBankService _bankService;
    private readonly ILearningOutcomeService _loService;
    private readonly IExamBalanceCheckService _balanceService;
    private readonly SemaphoreSlim _opLock = new(1, 1);

    public event Action? ExamSaved;

    [ObservableProperty] private ObservableCollection<Course> _courses = new();
    [ObservableProperty] private Course? _selectedCourse;

    // Sınav alanları
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private DateTime _examDate = DateTime.Today;
    [ObservableProperty] private int _durationMinutes = 60;
    [ObservableProperty] private ExamType _examType = ExamType.Midterm;
    [ObservableProperty] private int _bookletCount = 1;
    [ObservableProperty] private bool _shuffleOptions = false;
    [ObservableProperty] private int? _cutoffWeek = 7;

    // Soru havuzu (sol kolon)
    [ObservableProperty] private ObservableCollection<QuestionBankItemDto> _bankPool = new();
    [ObservableProperty] private string _bankSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<QuestionTypeFilterOption> _poolQuestionTypeFilters = new()
    {
        new QuestionTypeFilterOption("Tümü", null),
        new QuestionTypeFilterOption("Test", QuestionType.MultipleChoice),
        new QuestionTypeFilterOption("Klasik", QuestionType.OpenEnded)
    };
    [ObservableProperty] private QuestionTypeFilterOption? _selectedPoolQuestionTypeFilter;
    [ObservableProperty] private ObservableCollection<LearningOutcomeDto> _availableOutcomes = new();
    [ObservableProperty] private QuestionBankItemDto? _detailQuestion;
    [ObservableProperty] private QuestionBankCreateModel? _detailQuestionModel;

    // Seçili sorular (sağ kolon)
    [ObservableProperty] private ObservableCollection<QuestionBankItemDto> _selectedQuestions = new();
    [ObservableProperty] private decimal _selectedQuestionsTotalPoints;

    // Denge önizlemesi
    [ObservableProperty] private ExamBalanceReportDto? _balancePreview;

    // ── ÖĞRENCİ FİLTRESİ — ÇOKLU SINIF / BÖLÜM ──
    [ObservableProperty] private ObservableCollection<StudentSelectionViewModel> _allStudents = new();
    [ObservableProperty] private ObservableCollection<StudentSelectionViewModel> _filteredStudents = new();

    /// <summary>Sınıf adlarını CheckBox ile çoklu seçim olarak gösteren liste.</summary>
    [ObservableProperty] private ObservableCollection<ClassFilterItem> _classFilters = new();

    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private string? _courseError;
    [ObservableProperty] private string? _titleError;
    [ObservableProperty] private string? _durationMinutesError;
    [ObservableProperty] private string? _selectedQuestionsError;

    public ExamFromBankViewModel(
        IExamCrudService examService,
        IQuestionBankService bankService,
        ILearningOutcomeService loService,
        IExamBalanceCheckService balanceService)
    {
        _examService = examService;
        _bankService = bankService;
        _loService = loService;
        _balanceService = balanceService;
        SelectedPoolQuestionTypeFilter = PoolQuestionTypeFilters.FirstOrDefault();
    }

    public async Task LoadAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        DetachClassFilterHandlers();

        try
        {
            var courses = await _examService.GetCoursesAsync();
            Courses = new ObservableCollection<Course>(courses);
            if (SelectedCourse == null && courses.Count > 0)
                SelectedCourse = courses[0];

            var students = await _examService.GetStudentsAsync();
            var studentVms = students.Select(s => new StudentSelectionViewModel
            {
                Id = s.Id,
                StudentNumber = s.StudentNumber,
                FullName = s.FullName,
                ClassName = s.ClassName,
                IsSelected = false
            }).ToList();

            AllStudents = new ObservableCollection<StudentSelectionViewModel>(studentVms);

            // Sınıf filtresi (çoklu seçim için her bölüm/sınıf bir CheckBox)
            var classes = students.Select(s => s.ClassName).Distinct().OrderBy(c => c).ToList();
            var filterItems = classes.Select(c => new ClassFilterItem { ClassName = c, IsSelected = false }).ToList();

            foreach (var item in filterItems)
                item.PropertyChanged += OnClassFilterChanged;

            ClassFilters = new ObservableCollection<ClassFilterItem>(filterItems);
            ApplyClassFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Yükleme hatası: {ex.Message}";
        }
    }

    private void DetachClassFilterHandlers()
    {
        if (ClassFilters == null) return;
        foreach (var item in ClassFilters)
            item.PropertyChanged -= OnClassFilterChanged;
    }

    private void OnClassFilterChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClassFilterItem.IsSelected))
            ApplyClassFilter();
    }

    private void ApplyClassFilter()
    {
        var selectedClasses = ClassFilters.Where(c => c.IsSelected).Select(c => c.ClassName).ToHashSet();

        if (selectedClasses.Count == 0)
        {
            // Hiç filtre seçilmemişse tüm öğrenciler görünür
            FilteredStudents = new ObservableCollection<StudentSelectionViewModel>(AllStudents);
        }
        else
        {
            FilteredStudents = new ObservableCollection<StudentSelectionViewModel>(
                AllStudents.Where(s => selectedClasses.Contains(s.ClassName)));
        }
    }

    [RelayCommand]
    private void SelectAllClasses()
    {
        foreach (var item in ClassFilters) item.IsSelected = true;
    }

    [RelayCommand]
    private void ClearClassFilter()
    {
        foreach (var item in ClassFilters) item.IsSelected = false;
    }

    [RelayCommand]
    private void SelectAllFiltered()
    {
        foreach (var student in FilteredStudents)
            student.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAllFiltered()
    {
        foreach (var student in FilteredStudents)
            student.IsSelected = false;
    }

    partial void OnSelectedCourseChanged(Course? value)
    {
        CourseError = null;
        SelectedQuestions.Clear();
        RefreshSelectedPointsState();
        _ = ReloadAfterCourseChangeAsync();
    }

    partial void OnTitleChanged(string value) => TitleError = null;
    partial void OnDurationMinutesChanged(int value) => DurationMinutesError = null;
    partial void OnSelectedQuestionsTotalPointsChanged(decimal value) =>
        OnPropertyChanged(nameof(SelectedQuestionsPointSummary));

    public string SelectedQuestionsPointSummary => $"Toplam: {SelectedQuestionsTotalPoints:0.##} / 100 puan";

    /// <summary>Sıralı async — DbContext'e paralel sorgu gitmeyecek şekilde.</summary>
    private async Task ReloadAfterCourseChangeAsync()
    {
        await ReloadPoolAsync();
        await ReloadOutcomesAsync();
    }

    partial void OnExamTypeChanged(ExamType value)
    {
        _ = ReloadOutcomesAsync();
    }

    partial void OnSelectedPoolQuestionTypeFilterChanged(QuestionTypeFilterOption? value)
    {
        _ = ReloadPoolAsync();
    }

    private async Task ReloadOutcomesAsync()
    {
        if (SelectedCourse == null) return;
        await _opLock.WaitAsync();
        try
        {
            var list = await _loService.GetAvailableForExamAsync(SelectedCourse.Id, ExamType, CutoffWeek);
            AvailableOutcomes = new ObservableCollection<LearningOutcomeDto>(list);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"ÖÇ listesi yüklenemedi: {ex.Message}";
        }
        finally
        {
            _opLock.Release();
        }
    }

    [RelayCommand]
    private async Task SearchPoolAsync()
    {
        if (SelectedCourse == null) return;
        await _opLock.WaitAsync();
        try
        {
            var filter = new QuestionBankFilter
            {
                CourseId = SelectedCourse.Id,
                IsActive = true,
                Type = SelectedPoolQuestionTypeFilter?.Type,
                SearchText = string.IsNullOrWhiteSpace(BankSearchText) ? null : BankSearchText
            };
            var result = await _bankService.SearchAsync(filter);

            // Zaten seçilenleri havuzdan filtrele
            var selectedIds = SelectedQuestions.Select(q => q.Id).ToHashSet();
            BankPool = new ObservableCollection<QuestionBankItemDto>(
                result.Where(q => !selectedIds.Contains(q.Id)));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Havuz aranamadı: {ex.Message}";
        }
        finally
        {
            _opLock.Release();
        }
    }

    private Task ReloadPoolAsync() => SearchPoolAsync();

    [RelayCommand]
    private async Task ShowQuestionDetailAsync(QuestionBankItemDto? q)
    {
        if (q == null) return;

        await _opLock.WaitAsync();
        try
        {
            DetailQuestion = q;
            DetailQuestionModel = await _bankService.GetEditModelAsync(q.Id);
            ErrorMessage = DetailQuestionModel == null ? "Soru detayı bulunamadı." : null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Soru detayı yüklenemedi: {ex.Message}";
        }
        finally
        {
            _opLock.Release();
        }
    }

    [RelayCommand]
    private void CloseQuestionDetail()
    {
        DetailQuestion = null;
        DetailQuestionModel = null;
    }

    [RelayCommand]
    private async Task AddToExamAsync(QuestionBankItemDto? q)
    {
        if (q == null) return;
        SelectedQuestionsError = null;
        SelectedQuestions.Add(q);
        BankPool.Remove(q);
        RefreshSelectedPointsState();
        await UpdateBalancePreviewAsync();
    }

    [RelayCommand]
    private async Task RemoveFromExamAsync(QuestionBankItemDto? q)
    {
        if (q == null) return;
        SelectedQuestions.Remove(q);
        RefreshSelectedPointsState();
        if (SelectedQuestions.Count == 0)
            SelectedQuestionsError = "En az bir soru seçilmeli.";
        BankPool.Add(q);
        await UpdateBalancePreviewAsync();
    }

    [RelayCommand]
    private void MoveUp(QuestionBankItemDto? q)
    {
        if (q == null) return;
        var idx = SelectedQuestions.IndexOf(q);
        if (idx > 0) SelectedQuestions.Move(idx, idx - 1);
    }

    [RelayCommand]
    private void MoveDown(QuestionBankItemDto? q)
    {
        if (q == null) return;
        var idx = SelectedQuestions.IndexOf(q);
        if (idx >= 0 && idx < SelectedQuestions.Count - 1)
            SelectedQuestions.Move(idx, idx + 1);
    }

    public bool TryUpdateSelectedQuestionPoints(QuestionBankItemDto? question, decimal newPoints)
    {
        if (question == null || !SelectedQuestions.Contains(question))
            return false;

        var normalizedPoints = Math.Round(newPoints, 2);
        if (normalizedPoints <= 0)
        {
            SelectedQuestionsError = "Soru puanı 0'dan büyük olmalı.";
            return false;
        }

        var otherTotal = SelectedQuestions
            .Where(q => q.Id != question.Id)
            .Sum(q => q.MaxPoints);
        var attemptedTotal = Math.Round(otherTotal + normalizedPoints, 2);

        if (attemptedTotal > 100m)
        {
            SelectedQuestionsError =
                $"Toplam puan 100'ü aşamaz. Girilen değerle toplam {attemptedTotal:0.##} puan oluyor.";
            return false;
        }

        question.MaxPoints = normalizedPoints;
        RefreshSelectedPointsState();
        ErrorMessage = null;
        SuccessMessage = null;
        return true;
    }

    private void RefreshSelectedPointsState()
    {
        SelectedQuestionsTotalPoints = Math.Round(SelectedQuestions.Sum(q => q.MaxPoints), 2);

        if (SelectedQuestions.Count == 0)
        {
            if (SelectedQuestionsError != "En az bir soru seçilmeli.")
                SelectedQuestionsError = null;
            return;
        }

        if (SelectedQuestions.Any(q => q.MaxPoints <= 0))
        {
            SelectedQuestionsError = "Soru puanları 0'dan büyük olmalı.";
            return;
        }

        if (SelectedQuestionsTotalPoints > 100m)
        {
            SelectedQuestionsError =
                $"Toplam puan 100'ü aşamaz. Şu an {SelectedQuestionsTotalPoints:0.##} puan.";
            return;
        }

        if (SelectedQuestionsError is not null &&
            (SelectedQuestionsError.Contains("puan", StringComparison.OrdinalIgnoreCase) ||
             SelectedQuestionsError.Contains("soru seçilmeli", StringComparison.OrdinalIgnoreCase)))
        {
            SelectedQuestionsError = null;
        }
    }

    /// <summary>
    /// Seçili sorulara 100/N puan paylaştırır. Bu değer kayıtta OverrideMaxPoints olur;
    /// soru bankasındaki orijinal puan korunur.
    /// </summary>
    [RelayCommand]
    private void DistributePointsTo100()
    {
        if (SelectedQuestions.Count == 0)
        {
            ErrorMessage = "Puan dağıtımı için önce soru seçmelisiniz.";
            return;
        }

        var pointsPerQuestion = Math.Round(100m / SelectedQuestions.Count, 2);
        var snapshot = SelectedQuestions.ToList();
        for (var i = 0; i < snapshot.Count; i++)
        {
            var points = i == snapshot.Count - 1
                ? 100m - pointsPerQuestion * (snapshot.Count - 1)
                : pointsPerQuestion;
            snapshot[i].MaxPoints = Math.Round(points, 2);
        }

        SelectedQuestions = new ObservableCollection<QuestionBankItemDto>(snapshot);
        RefreshSelectedPointsState();
        SuccessMessage = $"{snapshot.Count} sorunun toplam puanı 100 olacak şekilde paylaştırıldı.";
        ErrorMessage = null;
    }

    private async Task UpdateBalancePreviewAsync()
    {
        if (SelectedCourse == null || SelectedQuestions.Count == 0)
        {
            BalancePreview = null;
            return;
        }
        try
        {
            BalancePreview = await _balanceService.AnalyzeDraftAsync(
                SelectedCourse.Id,
                SelectedQuestions.Select(q => q.Id).ToList());
        }
        catch
        {
            BalancePreview = null;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (!ValidateForm())
            return;

        var selectedCourse = SelectedCourse;
        if (selectedCourse == null)
            return;

        var studentModels = AllStudents
            .Where(s => s.IsSelected)
            .Select(s => new StudentCreateModel
            {
                StudentNumber = s.StudentNumber,
                FullName = s.FullName,
                ClassName = s.ClassName
            })
            .ToList();

        IsSaving = true;
        try
        {
            var userId = await _examService.GetDefaultUserIdAsync();
            var model = new ExamFromBankCreateModel
            {
                CourseId = selectedCourse.Id,
                Title = Title,
                ExamDate = ExamDate,
                DurationMinutes = DurationMinutes,
                ExamType = ExamType,
                BookletCount = BookletCount,
                ShuffleOptions = ShuffleOptions,
                CreatedByUserId = userId,
                Students = studentModels,
                SelectedQuestions = SelectedQuestions
                    .Select((q, i) => new ExamBankQuestionRef
                    {
                        QuestionId = q.Id,
                        OrderInExam = i + 1,
                        OverrideMaxPoints = q.MaxPoints
                    })
                    .ToList()
            };

            var newId = await _examService.CreateExamFromBankAsync(model);
            SuccessMessage = $"✓ Sınav oluşturuldu (Id: {newId}). Dengeli skor: {BalancePreview?.BalanceScore:0.0}/100";

            Title = string.Empty;
            SelectedQuestions.Clear();
            RefreshSelectedPointsState();
            BalancePreview = null;
            
            foreach (var s in AllStudents) s.IsSelected = false;
            foreach (var c in ClassFilters) c.IsSelected = false;

            await ReloadPoolAsync();

            ExamSaved?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kayıt hatası: {ex.Message}" +
                           (ex.InnerException != null ? $" — {ex.InnerException.Message}" : "");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ClearFormErrors()
    {
        CourseError = null;
        TitleError = null;
        DurationMinutesError = null;
        SelectedQuestionsError = null;
    }

    private bool ValidateForm()
    {
        ClearFormErrors();

        if (SelectedCourse == null)
        {
            CourseError = "Ders seçmelisiniz.";
            ErrorMessage = CourseError;
            return false;
        }
        if (string.IsNullOrWhiteSpace(Title))
        {
            TitleError = "Sınav başlığı zorunlu.";
            ErrorMessage = "Sınav başlığı boş olamaz.";
            return false;
        }
        if (DurationMinutes <= 0)
        {
            DurationMinutesError = "Süre 0'dan büyük olmalı.";
            ErrorMessage = DurationMinutesError;
            return false;
        }
        if (SelectedQuestions.Count == 0)
        {
            SelectedQuestionsError = "En az bir soru seçilmeli.";
            ErrorMessage = SelectedQuestionsError;
            return false;
        }

        RefreshSelectedPointsState();
        if (SelectedQuestions.Any(q => q.MaxPoints <= 0) || SelectedQuestionsTotalPoints > 100m)
        {
            ErrorMessage = SelectedQuestionsError;
            return false;
        }

        return true;
    }
}
