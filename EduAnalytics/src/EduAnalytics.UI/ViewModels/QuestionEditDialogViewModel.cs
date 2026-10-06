using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Enums;

namespace EduAnalytics.UI.ViewModels;

/// <summary>
/// Soru bankasındaki bir sorunun temel alanlarını düzenlemek için modal dialog VM'i.
/// Topic ve öğrenim çıktısı bağlantıları korunur.
/// </summary>
public partial class QuestionEditDialogViewModel : ObservableObject
{
    private readonly IQuestionBankService _bankService;
    private QuestionBankCreateModel? _model;

    [ObservableProperty] private int _questionId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMultipleChoice))]
    [NotifyPropertyChangedFor(nameof(IsOpenEnded))]
    private QuestionType _type = QuestionType.MultipleChoice;

    [ObservableProperty] private string _questionText = string.Empty;
    [ObservableProperty] private decimal _maxPoints = 1.0m;

    [ObservableProperty] private string _optionA = string.Empty;
    [ObservableProperty] private string _optionB = string.Empty;
    [ObservableProperty] private string _optionC = string.Empty;
    [ObservableProperty] private string _optionD = string.Empty;
    [ObservableProperty] private string _optionE = string.Empty;
    [ObservableProperty] private OptionLetter _correctOption = OptionLetter.A;
    [ObservableProperty] private string _answerKey = string.Empty;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private bool _isFavorite;

    [ObservableProperty] private string? _errorMessage;

    public bool IsMultipleChoice => Type == QuestionType.MultipleChoice;
    public bool IsOpenEnded => Type == QuestionType.OpenEnded;

    public OptionLetter[] AvailableOptions { get; } =
    {
        OptionLetter.A,
        OptionLetter.B,
        OptionLetter.C,
        OptionLetter.D,
        OptionLetter.E
    };

    public event Action<bool>? Closed;

    public QuestionEditDialogViewModel(IQuestionBankService bankService)
    {
        _bankService = bankService;
    }

    public async Task LoadAsync(int questionId)
    {
        ErrorMessage = null;
        _model = await _bankService.GetEditModelAsync(questionId)
            ?? throw new InvalidOperationException($"Soru bulunamadı: {questionId}");

        QuestionId = questionId;
        Type = _model.Type;
        QuestionText = _model.QuestionText;
        MaxPoints = _model.MaxPoints;
        OptionA = _model.OptionA;
        OptionB = _model.OptionB;
        OptionC = _model.OptionC;
        OptionD = _model.OptionD;
        OptionE = _model.OptionE;
        CorrectOption = _model.CorrectOption == OptionLetter.Empty ? OptionLetter.A : _model.CorrectOption;
        AnswerKey = _model.AnswerKey ?? string.Empty;
        IsActive = _model.IsActive;
        IsFavorite = _model.IsFavorite;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_model == null)
        {
            ErrorMessage = "Düzenleme modeli yüklenmedi.";
            return;
        }

        if (string.IsNullOrWhiteSpace(QuestionText))
        {
            ErrorMessage = "Soru metni boş olamaz.";
            return;
        }

        if (MaxPoints <= 0)
        {
            ErrorMessage = "Puan 0'dan büyük olmalı.";
            return;
        }

        if (IsMultipleChoice)
        {
            if (string.IsNullOrWhiteSpace(OptionA) ||
                string.IsNullOrWhiteSpace(OptionB) ||
                string.IsNullOrWhiteSpace(OptionC) ||
                string.IsNullOrWhiteSpace(OptionD))
            {
                ErrorMessage = "Test sorularında en az A/B/C/D şıkları doldurulmalı.";
                return;
            }

            if (CorrectOption == OptionLetter.Empty)
            {
                ErrorMessage = "Doğru şık seçilmeli.";
                return;
            }
        }

        var updated = new QuestionBankCreateModel
        {
            CourseId = _model.CourseId,
            QuestionGroupId = _model.QuestionGroupId,
            Type = Type,
            QuestionText = QuestionText,
            MaxPoints = MaxPoints,
            OptionA = IsMultipleChoice ? OptionA : string.Empty,
            OptionB = IsMultipleChoice ? OptionB : string.Empty,
            OptionC = IsMultipleChoice ? OptionC : string.Empty,
            OptionD = IsMultipleChoice ? OptionD : string.Empty,
            OptionE = IsMultipleChoice ? OptionE : string.Empty,
            CorrectOption = IsMultipleChoice ? CorrectOption : OptionLetter.Empty,
            AnswerKey = IsOpenEnded ? AnswerKey : null,
            IsActive = IsActive,
            IsFavorite = IsFavorite,
            CreatedByUserId = _model.CreatedByUserId,
            TopicIds = _model.TopicIds,
            LearningOutcomeIds = _model.LearningOutcomeIds
        };

        try
        {
            await _bankService.UpdateAsync(QuestionId, updated);
            Closed?.Invoke(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kayıt hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Closed?.Invoke(false);
    }
}
