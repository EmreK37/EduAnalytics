using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using EduAnalytics.Core.Enums;
using Microsoft.Win32;

namespace EduAnalytics.UI.ViewModels;

/// <summary>
/// Common-Stem (ortak gövdeli) soru grubu oluşturma ekranı.
/// Hoca bir tablo / paragraf / şema gövdesi yazar, ardından bu gövdeye bağlı 2+ soru ekler.
/// Sorular soru bankasına IsActive=true olarak yazılır.
/// </summary>
public partial class QuestionGroupEditorViewModel : ObservableObject
{
    private readonly IQuestionBankService _bankService;
    private readonly IExamCrudService _examService;

    [ObservableProperty] private ObservableCollection<Course> _courses = new();
    [ObservableProperty] private Course? _selectedCourse;

    // Dersin konuları (alt sorulara dağıtılır)
    [ObservableProperty] private ObservableCollection<EduAnalytics.Core.Entities.Topic> _availableTopics = new();

    // Gövde (stem) alanları
    [ObservableProperty] private string _stemText = string.Empty;
    [ObservableProperty] private string? _mediaPath;

    // Gruba bağlı sorular
    [ObservableProperty] private ObservableCollection<QuestionEditViewModel> _subQuestions = new();

    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private string? _courseError;
    [ObservableProperty] private string? _stemTextError;
    [ObservableProperty] private string? _subQuestionsError;

    public QuestionGroupEditorViewModel(IQuestionBankService bankService, IExamCrudService examService)
    {
        _bankService = bankService;
        _examService = examService;
    }

    public async Task LoadAsync()
    {
        try
        {
            var courses = await _examService.GetCoursesAsync();
            Courses = new ObservableCollection<Course>(courses);
            if (SelectedCourse == null && courses.Count > 0)
                SelectedCourse = courses[0];
            else
                await ReloadTopicsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Dersler yüklenemedi: {ex.Message}";
        }
    }

    partial void OnSelectedCourseChanged(Course? value)
    {
        CourseError = null;
        _ = ReloadTopicsAsync();
    }

    partial void OnStemTextChanged(string value) => StemTextError = null;

    private async Task ReloadTopicsAsync()
    {
        if (SelectedCourse == null) return;
        try
        {
            var topics = await _examService.GetTopicsForCourseAsync(SelectedCourse.Id);
            AvailableTopics = new ObservableCollection<EduAnalytics.Core.Entities.Topic>(topics);

            // Mevcut alt sorulara da yeni topic listesini ver
            foreach (var q in SubQuestions)
            {
                q.AvailableTopics.Clear();
                foreach (var t in topics) q.AvailableTopics.Add(t);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Konular yüklenemedi: {ex.Message}";
        }
    }

    [RelayCommand]
    private void BrowseMedia()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Görseller (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|PDF|*.pdf|Tüm Dosyalar|*.*",
            Title = "Şema / Görsel Seç"
        };
        if (dlg.ShowDialog() == true)
            MediaPath = dlg.FileName;
    }

    [RelayCommand]
    private void ClearMedia() => MediaPath = null;

    [RelayCommand]
    private void AddSubQuestion()
    {
        SubQuestionsError = null;
        var q = new QuestionEditViewModel
        {
            QuestionNumber = SubQuestions.Count + 1
        };
        // Yeni soruya mevcut konuları ata
        foreach (var t in AvailableTopics) q.AvailableTopics.Add(t);
        SubQuestions.Add(q);
    }

    [RelayCommand]
    private void RemoveSubQuestion(QuestionEditViewModel? q)
    {
        if (q == null) return;
        SubQuestions.Remove(q);
        if (SubQuestions.Count < 2)
            SubQuestionsError = "Common-stem grubu için en az 2 alt soru gerekli.";
        for (int i = 0; i < SubQuestions.Count; i++)
            SubQuestions[i].QuestionNumber = i + 1;
    }

    private void ClearFormErrors()
    {
        CourseError = null;
        StemTextError = null;
        SubQuestionsError = null;
        foreach (var q in SubQuestions)
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
        if (string.IsNullOrWhiteSpace(StemText))
        {
            StemTextError = "Gövde metni zorunlu.";
            ErrorMessage = "Gövde metni boş olamaz (tablo/paragraf/açıklama).";
            return false;
        }
        if (SubQuestions.Count < 2)
        {
            SubQuestionsError = "En az 2 alt soru eklemelisiniz.";
            ErrorMessage = "Common-stem grubu için en az 2 alt soru eklemelisiniz.";
            return false;
        }

        foreach (var q in SubQuestions)
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
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (!ValidateForm())
            return;

        IsSaving = true;
        try
        {
            var userId = await _examService.GetDefaultUserIdAsync();

            // 1) Grubu oluştur
            var groupId = await _bankService.CreateGroupAsync(new QuestionGroupCreateModel
            {
                CourseId = SelectedCourse.Id,
                StemText = StemText,
                MediaPath = MediaPath,
                CreatedByUserId = userId
            });

            // 2) Alt soruları soru bankasına ekle (her biri grupId'ye bağlı)
            int saved = 0;
            foreach (var q in SubQuestions)
            {
                await _bankService.CreateAsync(new QuestionBankCreateModel
                {
                    CourseId = SelectedCourse.Id,
                    QuestionGroupId = groupId,
                    Type = q.Type,
                    MaxPoints = q.MaxPoints,
                    QuestionText = q.QuestionText,
                    OptionA = q.IsMultipleChoice ? q.OptionA : string.Empty,
                    OptionB = q.IsMultipleChoice ? q.OptionB : string.Empty,
                    OptionC = q.IsMultipleChoice ? q.OptionC : string.Empty,
                    OptionD = q.IsMultipleChoice ? q.OptionD : string.Empty,
                    OptionE = q.IsMultipleChoice ? q.OptionE : string.Empty,
                    CorrectOption = q.IsMultipleChoice ? q.CorrectOption : OptionLetter.Empty,
                    AnswerKey = q.IsOpenEnded ? q.AnswerKey : null,
                    IsActive = true,
                    IsFavorite = false,
                    CreatedByUserId = userId,
                    TopicIds = q.SelectedTopics.Select(t => t.Id).ToList(),
                    LearningOutcomeIds = new List<int>()
                });
                saved++;
            }

            SuccessMessage = $"✓ Ortak gövde + {saved} alt soru kaydedildi (Grup #{groupId}).";

            // Temizle
            StemText = string.Empty;
            MediaPath = null;
            SubQuestions.Clear();
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
