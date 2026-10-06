using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Core.Entities;
using EduAnalytics.Core.Enums;
using EduAnalytics.Business.Dtos;

namespace EduAnalytics.UI.ViewModels;

/// <summary>
/// Sınav oluşturma formunda tek bir sorunun düzenlendiği alt ViewModel.
/// </summary>
public partial class QuestionEditViewModel : ObservableObject
{
    [ObservableProperty]
    private int _questionNumber;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMultipleChoice))]
    [NotifyPropertyChangedFor(nameof(IsOpenEnded))]
    private QuestionType _type = QuestionType.MultipleChoice;

    [ObservableProperty] private decimal _maxPoints = 1.0m;
    [ObservableProperty] private string _questionText = string.Empty;
    [ObservableProperty] private string _optionA = string.Empty;
    [ObservableProperty] private string _optionB = string.Empty;
    [ObservableProperty] private string _optionC = string.Empty;
    [ObservableProperty] private string _optionD = string.Empty;
    [ObservableProperty] private string _optionE = string.Empty;
    [ObservableProperty] private OptionLetter _correctOption = OptionLetter.A;
    [ObservableProperty] private string _answerKey = string.Empty;

    [ObservableProperty] private string? _questionTextError;
    [ObservableProperty] private string? _maxPointsError;
    [ObservableProperty] private string? _optionsError;
    [ObservableProperty] private string? _correctOptionError;

    /// <summary>UI'da gösterilecek şık harfleri (Test sorusunda radio button'lar için).</summary>
    public OptionLetter[] AvailableOptions { get; } =
        { OptionLetter.A, OptionLetter.B, OptionLetter.C, OptionLetter.D, OptionLetter.E };

    /// <summary>Mevcut ders için tüm konular (ortak referans).</summary>
    public ObservableCollection<Topic> AvailableTopics { get; } = new();

    /// <summary>Bu soruya bağlanmış konular.</summary>
    public ObservableCollection<Topic> SelectedTopics { get; set; } = new();

    [ObservableProperty]
    private Topic? _topicToAdd;

    // --- Learning Outcomes support ---
    /// <summary>Mevcut ders için tüm öğrenim çıktıları (ÖÇ) — UI için seçilebilir wrapper.</summary>
    public ObservableCollection<SelectableLearningOutcome> AvailableLearningOutcomes { get; } = new();

    /// <summary>Bu soruya bağlanmış öğrenim çıktıları (kayıtta kullanılır).</summary>
    public ObservableCollection<LearningOutcomeDto> SelectedLearningOutcomes { get; set; } = new();

    /// <summary>Filtrelenmiş ÖÇ görünümü — TextBox.Text → LearningOutcomeFilter ile bağlanır.</summary>
    public ICollectionView LearningOutcomesView { get; }

    [ObservableProperty]
    private string _learningOutcomeFilter = string.Empty;

    [ObservableProperty]
    private LearningOutcomeDto? _learningOutcomeToAdd;

    public QuestionEditViewModel()
    {
        LearningOutcomesView = CollectionViewSource.GetDefaultView(AvailableLearningOutcomes);
        LearningOutcomesView.Filter = obj =>
        {
            if (obj is not SelectableLearningOutcome item) return false;
            var filter = LearningOutcomeFilter?.Trim();
            if (string.IsNullOrEmpty(filter)) return true;
            return (item.Name?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                || (item.Code?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        };

        SelectedLearningOutcomes.CollectionChanged += (_, e) =>
        {
            if (e.OldItems != null)
                foreach (LearningOutcomeDto removed in e.OldItems)
                    AvailableLearningOutcomes.FirstOrDefault(w => w.Outcome.Id == removed.Id)?.SyncFromExternal(false);
            if (e.NewItems != null)
                foreach (LearningOutcomeDto added in e.NewItems)
                    AvailableLearningOutcomes.FirstOrDefault(w => w.Outcome.Id == added.Id)?.SyncFromExternal(true);
        };
    }

    partial void OnLearningOutcomeFilterChanged(string value) => LearningOutcomesView.Refresh();
    partial void OnQuestionTextChanged(string value) => QuestionTextError = null;
    partial void OnMaxPointsChanged(decimal value) => MaxPointsError = null;
    partial void OnOptionAChanged(string value) => OptionsError = null;
    partial void OnOptionBChanged(string value) => OptionsError = null;
    partial void OnOptionCChanged(string value) => OptionsError = null;
    partial void OnOptionDChanged(string value) => OptionsError = null;
    partial void OnOptionEChanged(string value) => OptionsError = null;
    partial void OnCorrectOptionChanged(OptionLetter value) => CorrectOptionError = null;

    // ----------------------------------

    public bool IsMultipleChoice => Type == QuestionType.MultipleChoice;
    public bool IsOpenEnded => Type == QuestionType.OpenEnded;

    [RelayCommand]
    private void AddTopic()
    {
        if (TopicToAdd != null && !SelectedTopics.Any(t => t.Id == TopicToAdd.Id))
        {
            SelectedTopics.Add(TopicToAdd);
        }
        TopicToAdd = null;
    }

    [RelayCommand]
    private void RemoveTopic(Topic? topic)
    {
        if (topic != null)
            SelectedTopics.Remove(topic);
    }

    [RelayCommand]
    private void AddLearningOutcome()
    {
        if (LearningOutcomeToAdd != null && !SelectedLearningOutcomes.Any(lo => lo.Id == LearningOutcomeToAdd.Id))
        {
            SelectedLearningOutcomes.Add(LearningOutcomeToAdd);
        }
        LearningOutcomeToAdd = null;
    }

    [RelayCommand]
    private void RemoveLearningOutcome(LearningOutcomeDto? lo)
    {
        if (lo != null)
            SelectedLearningOutcomes.Remove(lo);
    }

    /// <summary>
    /// Tür değiştiğinde varsayılan puanı akıllıca ayarla.
    /// </summary>
    partial void OnTypeChanged(QuestionType value)
    {
        ClearValidationErrors();

        if (value == QuestionType.MultipleChoice && MaxPoints > 5)
            MaxPoints = 1.0m;
        else if (value == QuestionType.OpenEnded && MaxPoints <= 1)
            MaxPoints = 5.0m;
    }

    public void ClearValidationErrors()
    {
        QuestionTextError = null;
        MaxPointsError = null;
        OptionsError = null;
        CorrectOptionError = null;
    }

    /// <summary>
    /// UI modelini servisin beklediği DTO'ya dönüştür.
    /// </summary>
    public Business.Dtos.QuestionCreateModel ToCreateModel()
    {
        return new Business.Dtos.QuestionCreateModel
        {
            QuestionNumber = QuestionNumber,
            Type = Type,
            MaxPoints = MaxPoints,
            QuestionText = QuestionText,
            OptionA = IsMultipleChoice ? OptionA : string.Empty,
            OptionB = IsMultipleChoice ? OptionB : string.Empty,
            OptionC = IsMultipleChoice ? OptionC : string.Empty,
            OptionD = IsMultipleChoice ? OptionD : string.Empty,
            OptionE = IsMultipleChoice ? OptionE : string.Empty,
            CorrectOption = IsMultipleChoice ? CorrectOption : OptionLetter.Empty,
            AnswerKey = IsOpenEnded ? AnswerKey : null,
            IsActive = true,
            IsFavorite = false,
            TopicIds = SelectedTopics.Select(t => t.Id).ToList(),
            LearningOutcomeIds = SelectedLearningOutcomes.Select(lo => lo.Id).ToList(),
            QuestionGroupId = null
        };
    }

    /// <summary>
    /// Form doğrulaması. Sorun varsa hata mesajı döner, yoksa null.
    /// </summary>
    public string? Validate()
    {
        ClearValidationErrors();

        if (string.IsNullOrWhiteSpace(QuestionText))
        {
            QuestionTextError = "Soru metni zorunlu.";
            return $"Soru {QuestionNumber}: Soru metni boş olamaz.";
        }
        if (MaxPoints <= 0)
        {
            MaxPointsError = "Puan 0'dan büyük olmalı.";
            return $"Soru {QuestionNumber}: Puan 0'dan büyük olmalı.";
        }

        if (IsMultipleChoice)
        {
            if (string.IsNullOrWhiteSpace(OptionA) || string.IsNullOrWhiteSpace(OptionB) ||
                string.IsNullOrWhiteSpace(OptionC) || string.IsNullOrWhiteSpace(OptionD) 
                || string.IsNullOrWhiteSpace(OptionE))
            {
                OptionsError = "Test sorularında A/B/C/D/E şıklarının tamamı doldurulmalı.";
                return $"Soru {QuestionNumber}: Tüm şıklar (A/B/C/D/E) doldurulmalı.";
            }
            if (CorrectOption == OptionLetter.Empty)
            {
                CorrectOptionError = "Doğru şık seçilmeli.";
                return $"Soru {QuestionNumber}: Doğru şık seçilmeli.";
            }
        }

        return null;
    }
}
