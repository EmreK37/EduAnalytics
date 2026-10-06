using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;

namespace EduAnalytics.UI.ViewModels;

/// <summary>
/// Akademik yapı yönetimi: Program → Ders → Konu hiyerarşisi.
/// Tek bir ekranda solda programlar, ortada seçili programın dersleri,
/// sağda seçili dersin konuları gösterilir. Her listede CRUD desteği var.
/// </summary>
public partial class AcademicStructureViewModel : ObservableObject
{
    private readonly IAcademicStructureService _service;
    private readonly IExamCrudService _examService;

    // ─── Program ───
    [ObservableProperty] private ObservableCollection<ProgramListDto> _programs = new();
    [ObservableProperty] private ProgramListDto? _selectedProgram;

    [ObservableProperty] private ProgramListDto? _editingProgram;
    [ObservableProperty] private string _programFormCode = string.Empty;
    [ObservableProperty] private string _programFormName = string.Empty;
    [ObservableProperty] private string _programFormDescription = string.Empty;

    // ─── Course ───
    [ObservableProperty] private ObservableCollection<CourseListDto> _courses = new();
    [ObservableProperty] private CourseListDto? _selectedCourse;

    [ObservableProperty] private CourseListDto? _editingCourse;
    [ObservableProperty] private string _courseFormCode = string.Empty;
    [ObservableProperty] private string _courseFormName = string.Empty;
    [ObservableProperty] private string _courseFormDescription = string.Empty;

    // ─── Topic ───
    [ObservableProperty] private ObservableCollection<TopicListDto> _topics = new();
    [ObservableProperty] private TopicListDto? _selectedTopic;

    [ObservableProperty] private TopicListDto? _editingTopic;
    [ObservableProperty] private int _topicFormWeek = 1;
    [ObservableProperty] private string _topicFormTitle = string.Empty;
    [ObservableProperty] private string _topicFormDescription = string.Empty;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    public AcademicStructureViewModel(IAcademicStructureService service, IExamCrudService examService)
    {
        _service = service;
        _examService = examService;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;
        try
        {
            await ReloadProgramsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Yükleme hatası: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ════════════════════════════════════════════════
    // PROGRAM
    // ════════════════════════════════════════════════

    private async Task ReloadProgramsAsync()
    {
        var list = await _service.GetProgramsAsync();
        Programs = new ObservableCollection<ProgramListDto>(list);
        if (SelectedProgram != null)
            SelectedProgram = Programs.FirstOrDefault(p => p.Id == SelectedProgram.Id);
        else if (Programs.Count > 0)
            SelectedProgram = Programs[0];
    }

    partial void OnSelectedProgramChanged(ProgramListDto? value)
    {
        SelectedCourse = null;
        Topics.Clear();
        _ = ReloadCoursesAsync();
    }

    [RelayCommand]
    private void StartNewProgram()
    {
        EditingProgram = null;
        ProgramFormCode = string.Empty;
        ProgramFormName = string.Empty;
        ProgramFormDescription = string.Empty;
        ClearMessages();
    }

    [RelayCommand]
    private void StartEditProgram(ProgramListDto? p)
    {
        if (p == null) return;
        EditingProgram = p;
        ProgramFormCode = p.Code;
        ProgramFormName = p.Name;
        ProgramFormDescription = p.Description ?? string.Empty;
        ClearMessages();
    }

    [RelayCommand]
    private async Task SaveProgramAsync()
    {
        ClearMessages();
        var model = new ProgramSaveModel
        {
            Code = ProgramFormCode,
            Name = ProgramFormName,
            Description = string.IsNullOrWhiteSpace(ProgramFormDescription) ? null : ProgramFormDescription
        };
        try
        {
            if (EditingProgram == null)
            {
                await _service.CreateProgramAsync(model);
                SuccessMessage = "✓ Program eklendi.";
            }
            else
            {
                await _service.UpdateProgramAsync(EditingProgram.Id, model);
                SuccessMessage = "✓ Program güncellendi.";
            }
            StartNewProgram();
            await ReloadProgramsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteProgramAsync(ProgramListDto? p)
    {
        if (p == null) return;
        var confirm = EduAnalytics.UI.Services.AppMessageBox.Show(
            $"'{p.Code} — {p.Name}' programını silmek istediğinize emin misiniz?",
            "Program Sil",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            await _service.DeleteProgramAsync(p.Id);
            SuccessMessage = "Program silindi.";
            await ReloadProgramsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    // ════════════════════════════════════════════════
    // COURSE
    // ════════════════════════════════════════════════

    private async Task ReloadCoursesAsync()
    {
        if (SelectedProgram == null)
        {
            Courses = new ObservableCollection<CourseListDto>();
            return;
        }
        var list = await _service.GetCoursesByProgramAsync(SelectedProgram.Id);
        Courses = new ObservableCollection<CourseListDto>(list);
        if (SelectedCourse != null)
            SelectedCourse = Courses.FirstOrDefault(c => c.Id == SelectedCourse.Id);
    }

    partial void OnSelectedCourseChanged(CourseListDto? value)
    {
        _ = ReloadTopicsAsync();
    }

    [RelayCommand]
    private void StartNewCourse()
    {
        EditingCourse = null;
        CourseFormCode = string.Empty;
        CourseFormName = string.Empty;
        CourseFormDescription = string.Empty;
        ClearMessages();
    }

    [RelayCommand]
    private void StartEditCourse(CourseListDto? c)
    {
        if (c == null) return;
        EditingCourse = c;
        CourseFormCode = c.Code;
        CourseFormName = c.Name;
        CourseFormDescription = c.Description ?? string.Empty;
        ClearMessages();
    }

    [RelayCommand]
    private async Task SaveCourseAsync()
    {
        ClearMessages();
        if (SelectedProgram == null)
        {
            ErrorMessage = "Önce bir program seçmelisiniz.";
            return;
        }

        var model = new CourseSaveModel
        {
            ProgramId = SelectedProgram.Id,
            Code = CourseFormCode,
            Name = CourseFormName,
            Description = string.IsNullOrWhiteSpace(CourseFormDescription) ? null : CourseFormDescription
        };

        try
        {
            if (EditingCourse == null)
            {
                var userId = await _examService.GetDefaultUserIdAsync();
                await _service.CreateCourseAsync(model, userId);
                SuccessMessage = "✓ Ders eklendi.";
            }
            else
            {
                await _service.UpdateCourseAsync(EditingCourse.Id, model);
                SuccessMessage = "✓ Ders güncellendi.";
            }
            StartNewCourse();
            await ReloadCoursesAsync();
            await ReloadProgramsAsync(); // CourseCount güncellensin
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteCourseAsync(CourseListDto? c)
    {
        if (c == null) return;
        var confirm = EduAnalytics.UI.Services.AppMessageBox.Show(
            $"'{c.Code} — {c.Name}' dersini silmek istediğinize emin misiniz?\n\nDersin tüm konuları otomatik silinir.",
            "Ders Sil",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            await _service.DeleteCourseAsync(c.Id);
            SuccessMessage = "Ders silindi.";
            await ReloadCoursesAsync();
            await ReloadProgramsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    // ════════════════════════════════════════════════
    // TOPIC
    // ════════════════════════════════════════════════

    private async Task ReloadTopicsAsync()
    {
        if (SelectedCourse == null)
        {
            Topics = new ObservableCollection<TopicListDto>();
            return;
        }
        var list = await _service.GetTopicsByCourseAsync(SelectedCourse.Id);
        Topics = new ObservableCollection<TopicListDto>(list);
    }

    [RelayCommand]
    private void StartNewTopic()
    {
        EditingTopic = null;
        TopicFormWeek = (Topics.Count > 0 ? Topics.Max(t => t.WeekNumber) : 0) + 1;
        TopicFormTitle = string.Empty;
        TopicFormDescription = string.Empty;
        ClearMessages();
    }

    [RelayCommand]
    private void StartEditTopic(TopicListDto? t)
    {
        if (t == null) return;
        EditingTopic = t;
        TopicFormWeek = t.WeekNumber;
        TopicFormTitle = t.Title;
        TopicFormDescription = t.Description ?? string.Empty;
        ClearMessages();
    }

    [RelayCommand]
    private async Task SaveTopicAsync()
    {
        ClearMessages();
        if (SelectedCourse == null)
        {
            ErrorMessage = "Önce bir ders seçmelisiniz.";
            return;
        }

        var model = new TopicSaveModel
        {
            CourseId = SelectedCourse.Id,
            WeekNumber = TopicFormWeek,
            Title = TopicFormTitle,
            Description = string.IsNullOrWhiteSpace(TopicFormDescription) ? null : TopicFormDescription
        };

        try
        {
            if (EditingTopic == null)
            {
                await _service.CreateTopicAsync(model);
                SuccessMessage = "✓ Konu eklendi.";
            }
            else
            {
                await _service.UpdateTopicAsync(EditingTopic.Id, model);
                SuccessMessage = "✓ Konu güncellendi.";
            }
            StartNewTopic();
            await ReloadTopicsAsync();
            await ReloadCoursesAsync(); // TopicCount güncellensin
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteTopicAsync(TopicListDto? t)
    {
        if (t == null) return;
        var confirm = EduAnalytics.UI.Services.AppMessageBox.Show(
            $"'Hafta {t.WeekNumber} — {t.Title}' konusunu silmek istediğinize emin misiniz?",
            "Konu Sil",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            await _service.DeleteTopicAsync(t.Id);
            SuccessMessage = "Konu silindi.";
            await ReloadTopicsAsync();
            await ReloadCoursesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void ClearMessages()
    {
        ErrorMessage = null;
        SuccessMessage = null;
    }
}
