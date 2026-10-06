using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace EduAnalytics.UI.ViewModels;

public partial class QuestionBankViewModel : ObservableObject
{
    private readonly IQuestionBankService _bankService;
    private readonly IExamCrudService _examService;

    /// <summary>DbContext'e paralel sorgu gitmesini engellemek için search lock'u.</summary>
    private readonly SemaphoreSlim _searchLock = new(1, 1);

    /// <summary>SearchText değişiminde debounce için.</summary>
    private CancellationTokenSource? _searchDebounceCts;

    [ObservableProperty] private ObservableCollection<Course> _courses = new();
    [ObservableProperty] private Course? _selectedCourse;

    [ObservableProperty] private ObservableCollection<QuestionBankItemDto> _questions = new();

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _filterStatus = "Tümü";   // "Tümü" | "Aktif" | "Pasif"
    [ObservableProperty] private bool _filterFavoriteOnly;
    [ObservableProperty] private string _typeFilter = "Tümü";     // "Tümü" | "Test" | "Klasik"

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    // Yeni eklenecek command
    public event Action? QuestionCreateRequested;

    public QuestionBankViewModel(IQuestionBankService bankService, IExamCrudService examService)
    {
        _bankService = bankService;
        _examService = examService;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;
        try
        {
            var courses = await _examService.GetCoursesAsync();
            Courses = new ObservableCollection<Course>(courses);

            if (SelectedCourse == null && courses.Count > 0)
                SelectedCourse = courses[0];   // OnSelectedCourseChanged tetikleyecek
            else
                await SearchSafelyAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Veriler yüklenemedi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Filter property'leri değiştiğinde otomatik aramayı tetikle ──
    partial void OnSelectedCourseChanged(Course? value) => _ = SearchSafelyAsync();
    partial void OnTypeFilterChanged(string value) => _ = SearchSafelyAsync();
    partial void OnFilterStatusChanged(string value) => _ = SearchSafelyAsync();
    partial void OnFilterFavoriteOnlyChanged(bool value) => _ = SearchSafelyAsync();

    /// <summary>Kullanıcı yazarken her tuş için DB'ye gitmeyelim — 300ms debounce.</summary>
    partial void OnSearchTextChanged(string value)
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;
        _ = DebounceSearchAsync(token);
    }

    private async Task DebounceSearchAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);
            if (token.IsCancellationRequested) return;
            await SearchSafelyAsync();
        }
        catch (TaskCanceledException) { /* yeni tuş geldi, normal */ }
    }

    [RelayCommand]
    private Task SearchAsync() => SearchSafelyAsync();

    [RelayCommand]
    private void NavigateToCreateQuestion()
    {
        QuestionCreateRequested?.Invoke();
    }

    /// <summary>
    /// Tek seferde sadece bir search çalışır — DbContext concurrency hatasını engeller.
    /// </summary>
    private async Task SearchSafelyAsync()
    {
        if (SelectedCourse == null) return;

        await _searchLock.WaitAsync();
        try
        {
            ErrorMessage = null;

            bool? activeFilter = FilterStatus switch
            {
                "Aktif" => true,
                "Pasif" => false,
                _ => null
            };

            var filter = new QuestionBankFilter
            {
                CourseId = SelectedCourse.Id,
                IsActive = activeFilter,
                IsFavorite = FilterFavoriteOnly ? true : null,
                SearchText = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText
            };

            if (TypeFilter == "Test") filter.Type = Core.Enums.QuestionType.MultipleChoice;
            else if (TypeFilter == "Klasik") filter.Type = Core.Enums.QuestionType.OpenEnded;

            var list = await _bankService.SearchAsync(filter);
            Questions = new ObservableCollection<QuestionBankItemDto>(list);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Arama hatası: {ex.Message}";
        }
        finally
        {
            _searchLock.Release();
        }
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(QuestionBankItemDto? item)
    {
        if (item == null) return;
        await _searchLock.WaitAsync();
        try
        {
            await _bankService.ToggleActiveAsync(item.Id);
            item.IsActive = !item.IsActive;
            var idx = Questions.IndexOf(item);
            if (idx >= 0)
            {
                Questions.RemoveAt(idx);
                Questions.Insert(idx, item);
            }
            SuccessMessage = $"Soru {(item.IsActive ? "aktifleştirildi" : "pasifleştirildi")}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _searchLock.Release();
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(QuestionBankItemDto? item)
    {
        if (item == null) return;
        await _searchLock.WaitAsync();
        try
        {
            await _bankService.ToggleFavoriteAsync(item.Id);
            item.IsFavorite = !item.IsFavorite;
            var idx = Questions.IndexOf(item);
            if (idx >= 0)
            {
                Questions.RemoveAt(idx);
                Questions.Insert(idx, item);
            }
            SuccessMessage = item.IsFavorite ? "★ Favorilere eklendi" : "Favorilerden çıkarıldı";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _searchLock.Release();
        }
    }

    [RelayCommand]
    private async Task EditAsync(QuestionBankItemDto? item)
    {
        if (item == null) return;

        try
        {
            var dialogVm = App.Services.GetRequiredService<QuestionEditDialogViewModel>();
            await dialogVm.LoadAsync(item.Id);

            var dialog = new Views.QuestionEditDialog(dialogVm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            var result = dialog.ShowDialog();
            if (result == true)
            {
                SuccessMessage = "Soru güncellendi.";
                await SearchSafelyAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Düzenleme hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(QuestionBankItemDto? item)
    {
        if (item == null) return;
        var confirm = EduAnalytics.UI.Services.AppMessageBox.Show(
            $"Soruyu silmek istediğinize emin misiniz?\n\n\"{item.QuestionText[..Math.Min(item.QuestionText.Length, 80)]}\"",
            "Soru Sil",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        await _searchLock.WaitAsync();
        try
        {
            await _bankService.DeleteAsync(item.Id);
            Questions.Remove(item);
            SuccessMessage = "Soru silindi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _searchLock.Release();
        }
    }
}
