using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using EduAnalytics.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace EduAnalytics.UI.ViewModels;

public partial class ExamCreateViewModel : ObservableObject
{
    private readonly IExamCrudService _service;
    private readonly SemaphoreSlim _opLock = new(1, 1);

    public event Action? ExamSaved;

    [ObservableProperty] private ObservableCollection<Course> _courses = new();
    [ObservableProperty] private ObservableCollection<Topic> _availableTopics = new();
    
    // Add available learning outcomes property at the exam level
    [ObservableProperty] private ObservableCollection<LearningOutcomeDto> _availableLearningOutcomes = new();

    [ObservableProperty]
    private Course? _selectedCourse;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private DateTime _examDate = DateTime.Today;

    // ASOS yenileme: sınav süresi, tipi ve kitapçık ayarları
    [ObservableProperty] private int _durationMinutes = 60;
    [ObservableProperty] private ExamType _examType = ExamType.Midterm;
    [ObservableProperty] private int _bookletCount = 1;
    [ObservableProperty] private bool _shuffleOptions = false;

    [ObservableProperty] private ObservableCollection<QuestionEditViewModel> _questions = new();

    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private string? _courseError;
    [ObservableProperty] private string? _titleError;
    [ObservableProperty] private string? _durationMinutesError;
    [ObservableProperty] private string? _questionsError;

    // ── ÖĞRENCİ FİLTRESİ — ÇOKLU SINIF / BÖLÜM ──
    [ObservableProperty] private ObservableCollection<StudentSelectionViewModel> _allStudents = new();
    [ObservableProperty] private ObservableCollection<StudentSelectionViewModel> _filteredStudents = new();

    /// <summary>Sınıf adlarını CheckBox ile çoklu seçim olarak gösteren liste.</summary>
    [ObservableProperty] private ObservableCollection<ClassFilterItem> _classFilters = new();

    public ExamCreateViewModel(IExamCrudService service)
    {
        _service = service;
    }

    public async Task LoadAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        // Eski ClassFilters varsa event handler'larını temizle (leak önleme)
        DetachClassFilterHandlers();

        await _opLock.WaitAsync();
        try
        {
            // Önce kursları + öğrencileri yükle, SelectedCourse atamasını sona bırak.
            // Aksi halde OnSelectedCourseChanged → ReloadTopicsAsync paralel sorgu üretir.
            var courses = await _service.GetCoursesAsync();
            Courses = new ObservableCollection<Course>(courses);

            var students = await _service.GetStudentsAsync();
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
            ErrorMessage = $"Veriler yüklenemedi: {ex.Message}";
        }
        finally
        {
            _opLock.Release();
        }

        // Course atamasını lock dışında yap — OnSelectedCourseChanged kendi lock'ını alır
        if (SelectedCourse == null && Courses.Count > 0)
            SelectedCourse = Courses[0];
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
        _ = ReloadLearningOutcomesAsync();
    }

    partial void OnTitleChanged(string value) => TitleError = null;
    partial void OnDurationMinutesChanged(int value) => DurationMinutesError = null;

    private async Task ReloadLearningOutcomesAsync()
    {
        if (SelectedCourse == null)
        {
            return;
        }

        await _opLock.WaitAsync();
        try
        {
            if (App.Services.GetService<ILearningOutcomeService>() is ILearningOutcomeService service)
            {
                var outcomes = await service.GetByCourseAsync(SelectedCourse.Id);
                AvailableLearningOutcomes = new ObservableCollection<LearningOutcomeDto>(outcomes);
                foreach (var q in Questions)
                {
                    q.AvailableLearningOutcomes.Clear();
                    foreach (var lo in outcomes)
                        q.AvailableLearningOutcomes.Add(new SelectableLearningOutcome(lo, q.SelectedLearningOutcomes));
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Öğrenim çıktıları yüklenemedi: {ex.Message}";
        }
        finally
        {
            _opLock.Release();
        }
    }

    private async Task ReloadTopicsAsync()
    {
        if (SelectedCourse == null)
        {
            AvailableTopics.Clear();
            return;
        }

        await _opLock.WaitAsync();
        try
        {
            var topics = await _service.GetTopicsForCourseAsync(SelectedCourse.Id);
            AvailableTopics = new ObservableCollection<Topic>(topics);

            foreach (var q in Questions)
            {
                q.AvailableTopics.Clear();
                foreach (var t in topics) q.AvailableTopics.Add(t);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Konular yüklenemedi: {ex.Message}";
        }
        finally
        {
            _opLock.Release();
        }
    }

    [RelayCommand]
    private void AddQuestion()
    {
        QuestionsError = null;
        var q = new QuestionEditViewModel
        {
            QuestionNumber = Questions.Count + 1
        };
        foreach (var t in AvailableTopics) q.AvailableTopics.Add(t);

        foreach (var lo in AvailableLearningOutcomes)
            q.AvailableLearningOutcomes.Add(new SelectableLearningOutcome(lo, q.SelectedLearningOutcomes));

        Questions.Add(q);
    }

    [RelayCommand]
    private void RemoveQuestion(QuestionEditViewModel? q)
    {
        if (q != null)
        {
            Questions.Remove(q);
            if (Questions.Count == 0)
                QuestionsError = "En az bir soru eklemelisiniz.";
            for (int i = 0; i < Questions.Count; i++)
                Questions[i].QuestionNumber = i + 1;
        }
    }

    private void ClearFormErrors()
    {
        CourseError = null;
        TitleError = null;
        DurationMinutesError = null;
        QuestionsError = null;
        foreach (var q in Questions)
            q.ClearValidationErrors();
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
        if (Questions.Count == 0)
        {
            QuestionsError = "En az bir soru eklemelisiniz.";
            ErrorMessage = QuestionsError;
            return false;
        }

        foreach (var q in Questions)
        {
            var err = q.Validate();
            if (err != null)
            {
                ErrorMessage = err;
                return false;
            }
        }

        return true;
    }

    [RelayCommand]
    private void DistributePointsTo100()
    {
        if (Questions.Count == 0)
        {
            ErrorMessage = "Puan dağıtımı için en az bir soru eklemelisiniz.";
            return;
        }

        decimal pointsPerQuestion = 100m / Questions.Count;
        foreach (var q in Questions)
        {
            q.MaxPoints = Math.Round(pointsPerQuestion, 2);
        }

        SuccessMessage = $"✓ {Questions.Count} soruya ortalama {Math.Round(pointsPerQuestion, 2)} puan dağıtıldı.";
        ErrorMessage = null;
    }

    [RelayCommand]
    private void MoveQuestionUp(QuestionEditViewModel? q)
    {
        if (q == null) return;
        int index = Questions.IndexOf(q);
        if (index > 0)
        {
            Questions.Move(index, index - 1);
            for (int i = 0; i < Questions.Count; i++)
                Questions[i].QuestionNumber = i + 1;
        }
    }

    [RelayCommand]
    private void MoveQuestionDown(QuestionEditViewModel? q)
    {
        if (q == null) return;
        int index = Questions.IndexOf(q);
        if (index >= 0 && index < Questions.Count - 1)
        {
            Questions.Move(index, index + 1);
            for (int i = 0; i < Questions.Count; i++)
                Questions[i].QuestionNumber = i + 1;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (!ValidateForm())
            return;

        // Seçili öğrenciler (sınıf filtresi gözardı edilir, IsSelected esas)
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
            var userId = await _service.GetDefaultUserIdAsync();
            var model = new ExamCreateModel
            {
                CourseId = SelectedCourse.Id,
                Title = Title,
                ExamDate = ExamDate,
                DurationMinutes = DurationMinutes,
                ExamType = ExamType,
                BookletCount = BookletCount,
                ShuffleOptions = ShuffleOptions,
                CreatedByUserId = userId,
                Questions = Questions.Select(q => q.ToCreateModel()).ToList(),
                Students = studentModels
            };

            var newId = await _service.CreateExamAsync(model);
            SuccessMessage = $"Sınav başarıyla kaydedildi. Id: {newId}";

            Title = string.Empty;
            Questions.Clear();
            foreach (var s in AllStudents) s.IsSelected = false;
            foreach (var c in ClassFilters) c.IsSelected = false;

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
}
