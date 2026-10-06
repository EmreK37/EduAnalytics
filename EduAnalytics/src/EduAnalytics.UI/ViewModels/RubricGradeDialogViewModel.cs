using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;

namespace EduAnalytics.UI.ViewModels;

public partial class RubricGradeDialogViewModel : ObservableObject
{
    private readonly IRubricService _rubricService;

    private int _examId;
    private int _questionId;
    private int _studentId;

    [ObservableProperty] private string _questionTitle = string.Empty;
    [ObservableProperty] private string _studentTitle = string.Empty;
    [ObservableProperty] private decimal _totalScore;
    [ObservableProperty] private decimal _maxTotalScore;

    [ObservableProperty] private ObservableCollection<RubricGradeRowViewModel> _rows = new();

    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    public event Action? GradeSaved;

    public RubricGradeDialogViewModel(IRubricService rubricService)
    {
        _rubricService = rubricService;
    }

    public async Task LoadAsync(int examId, int questionId, int studentId,
                                string questionInfo, string studentInfo)
    {
        _examId = examId;
        _questionId = questionId;
        _studentId = studentId;
        QuestionTitle = questionInfo;
        StudentTitle = studentInfo;

        try
        {
            var grade = await _rubricService.GetStudentGradeAsync(examId, questionId, studentId);

            var rows = grade.CriterionScores.Select(c => new RubricGradeRowViewModel(this)
            {
                CriterionId = c.CriterionId,
                Title = c.CriterionTitle,
                MaxPoints = c.MaxPoints,
                Score = c.Score,
                Comment = c.Comment ?? string.Empty
            }).ToList();

            Rows = new ObservableCollection<RubricGradeRowViewModel>(rows);
            MaxTotalScore = rows.Sum(r => r.MaxPoints);
            RecomputeTotal();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Yükleme hatası: {ex.Message}";
        }
    }

    internal void RecomputeTotal()
    {
        TotalScore = Rows.Sum(r => r.Score);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        // Her kriter sınırı içinde mi?
        foreach (var r in Rows)
        {
            if (r.Score < 0 || r.Score > r.MaxPoints)
            {
                ErrorMessage = $"\"{r.Title}\" puanı 0 — {r.MaxPoints} arasında olmalı.";
                return;
            }
        }

        IsSaving = true;
        try
        {
            var updates = Rows.Select(r => new CriterionScoreUpdate
            {
                CriterionId = r.CriterionId,
                Score = r.Score,
                Comment = string.IsNullOrWhiteSpace(r.Comment) ? null : r.Comment
            }).ToList();

            await _rubricService.SaveStudentGradeAsync(_examId, _questionId, _studentId, updates);
            SuccessMessage = $"✓ Kaydedildi. Toplam: {TotalScore}/{MaxTotalScore}";
            GradeSaved?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kaydetme hatası: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }
}

public partial class RubricGradeRowViewModel : ObservableObject
{
    private readonly RubricGradeDialogViewModel _parent;

    public int CriterionId { get; set; }
    public string Title { get; set; } = null!;
    public decimal MaxPoints { get; set; }

    [ObservableProperty] private decimal _score;
    [ObservableProperty] private string _comment = string.Empty;

    public RubricGradeRowViewModel(RubricGradeDialogViewModel parent)
    {
        _parent = parent;
    }

    partial void OnScoreChanged(decimal value)
    {
        _parent.RecomputeTotal();
    }
}
