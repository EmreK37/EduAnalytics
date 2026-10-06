using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Enums;

namespace EduAnalytics.UI.ViewModels;

/// <summary>
/// Tüm sınavları listeler; temel sınav bilgisini düzenler ve sınavı silebilir.
/// </summary>
public partial class ExamManagementViewModel : ObservableObject
{
    private readonly IExamCrudService _examService;

    [ObservableProperty] private ObservableCollection<ExamListItemDto> _exams = new();

    [ObservableProperty] private bool _isEditDialogOpen;
    [ObservableProperty] private int _editingExamId;
    [ObservableProperty] private string _editTitle = string.Empty;
    [ObservableProperty] private DateTime _editExamDate = DateTime.Today;
    [ObservableProperty] private int _editDurationMinutes = 60;
    [ObservableProperty] private ExamType _editExamType = ExamType.Midterm;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    public event Action<int>? OpenAnalysisRequested;

    public ExamManagementViewModel(IExamCrudService examService)
    {
        _examService = examService;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var list = await _examService.GetAllExamsAsync();
            Exams = new ObservableCollection<ExamListItemDto>(list);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Sınav listesi yüklenemedi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenAnalysis(ExamListItemDto? item)
    {
        if (item != null)
            OpenAnalysisRequested?.Invoke(item.Id);
    }

    [RelayCommand]
    private async Task EditAsync(ExamListItemDto? item)
    {
        if (item == null) return;

        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var model = await _examService.GetExamForEditAsync(item.Id)
                ?? throw new InvalidOperationException("Sınav bulunamadı.");

            EditingExamId = model.Id;
            EditTitle = model.Title;
            EditExamDate = model.ExamDate;
            EditDurationMinutes = model.DurationMinutes;
            EditExamType = model.ExamType;
            IsEditDialogOpen = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Düzenleme yüklenemedi: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        if (string.IsNullOrWhiteSpace(EditTitle))
        {
            ErrorMessage = "Sınav başlığı boş olamaz.";
            return;
        }

        try
        {
            await _examService.UpdateExamAsync(new ExamUpdateModel
            {
                Id = EditingExamId,
                Title = EditTitle,
                ExamDate = EditExamDate,
                DurationMinutes = EditDurationMinutes,
                ExamType = EditExamType
            });

            IsEditDialogOpen = false;
            await LoadAsync();
            SuccessMessage = "Sınav güncellendi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Güncelleme hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditDialogOpen = false;
    }

    [RelayCommand]
    private async Task DeleteAsync(ExamListItemDto? item)
    {
        if (item == null) return;

        var warning = item.HasAnswers
            ? $"\n\nBu sınava {item.TotalAnswers} cevap girilmiş. Silinince bu cevaplar da kaybolur."
            : string.Empty;

        var confirm = EduAnalytics.UI.Services.AppMessageBox.Show(
            $"\"{item.Title}\" sınavını silmek istediğinize emin misiniz?{warning}",
            "Sınav Sil",
            System.Windows.MessageBoxButton.YesNo,
            item.HasAnswers ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            await _examService.DeleteExamAsync(item.Id);
            Exams.Remove(item);
            SuccessMessage = "Sınav silindi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Silme hatası: {ex.Message}";
        }
    }
}
