using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;

namespace EduAnalytics.UI.ViewModels;

public partial class ProgramOutcomeReportViewModel : ObservableObject
{
    private readonly IProgramOutcomeService _poService;
    private readonly SemaphoreSlim _opLock = new(1, 1);

    [ObservableProperty] private ObservableCollection<Program> _programs = new();
    [ObservableProperty] private Program? _selectedProgram;
    [ObservableProperty] private ObservableCollection<ProgramOutcomeReportDto> _reports = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public ProgramOutcomeReportViewModel(IProgramOutcomeService poService)
    {
        _poService = poService;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var programs = await _poService.GetProgramsAsync();
            Programs = new ObservableCollection<Program>(programs);

            if (SelectedProgram == null && programs.Count > 0)
                SelectedProgram = programs[0];
            else
                await GenerateReportAsync();
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

    partial void OnSelectedProgramChanged(Program? value)
    {
        _ = GenerateReportAsync();
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        if (SelectedProgram == null) return;

        await _opLock.WaitAsync();
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var reports = await _poService.GenerateReportAsync(SelectedProgram.Id);
            Reports = new ObservableCollection<ProgramOutcomeReportDto>(reports);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Rapor üretilemedi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _opLock.Release();
        }
    }
}
