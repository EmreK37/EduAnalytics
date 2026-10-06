using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace EduAnalytics.UI.ViewModels;

public partial class SingleQuestionCreateViewModel : ObservableObject
{
    private readonly IExamCrudService _service;

    public event Action? QuestionSaved;

    [ObservableProperty] private ObservableCollection<Course> _courses = new();
    [ObservableProperty] private Course? _selectedCourse;

    [ObservableProperty] private QuestionEditViewModel _question = new();

    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _courseError;

    /// <summary>İkinci sekme: ortak gövdeli (common-stem) soru grubu editörü.</summary>
    public QuestionGroupEditorViewModel GroupEditor { get; }

    public SingleQuestionCreateViewModel(IExamCrudService service, QuestionGroupEditorViewModel groupEditor)
    {
        _service = service;
        GroupEditor = groupEditor;
        Question.QuestionNumber = 1;
    }

    public async Task LoadAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;
        try
        {
            var courses = await _service.GetCoursesAsync();
            Courses = new ObservableCollection<Course>(courses);
            if (SelectedCourse == null && Courses.Count > 0)
            {
                SelectedCourse = Courses[0];
            }

            // İlişkili Soru sekmesini de yükle
            await GroupEditor.LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Veriler yüklenemedi: " + ex.Message;
        }
    }

    partial void OnSelectedCourseChanged(Course? value)
    {
        CourseError = null;
        _ = ReloadTopicsAsync();
        Question.AvailableTopics.Clear();
        // Clear learning outcomes lists as well when course changes
        Question.AvailableLearningOutcomes.Clear();
        Question.SelectedLearningOutcomes.Clear();
    }

    private async Task ReloadTopicsAsync()
    {
        if (SelectedCourse == null) return;
        try
        {
            var topics = await _service.GetTopicsForCourseAsync(SelectedCourse.Id);
            Question.AvailableTopics.Clear();
            foreach (var t in topics) Question.AvailableTopics.Add(t);

            // Load learning outcomes for the selected course
            var loService = App.Services.GetService<ILearningOutcomeService>();
            if (loService != null)
            {
                var los = await loService.GetByCourseAsync(SelectedCourse.Id);
                Question.AvailableLearningOutcomes.Clear();
                foreach (var lo in los)
                    Question.AvailableLearningOutcomes.Add(new SelectableLearningOutcome(lo, Question.SelectedLearningOutcomes));
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;
        CourseError = null;
        Question.ClearValidationErrors();

        if (SelectedCourse == null)
        {
            CourseError = "Ders seçmelisiniz.";
            ErrorMessage = "Lütfen bir ders seçin.";
            return;
        }

        var err = Question.Validate();
        if (err != null)
        {
            ErrorMessage = err;
            return;
        }

        IsSaving = true;
        try
        {
            var userId = await _service.GetDefaultUserIdAsync();

            // DI üzerinden IQuestionBankService'i alalım
            var bankService = App.Services.GetService<IQuestionBankService>();

            if (bankService != null)
            {
                var qm = Question.ToCreateModel();

                var model = new QuestionBankCreateModel
                {
                    CourseId = SelectedCourse.Id,
                    QuestionGroupId = qm.QuestionGroupId,
                    Type = qm.Type,
                    MaxPoints = qm.MaxPoints,
                    QuestionText = qm.QuestionText,
                    OptionA = qm.OptionA,
                    OptionB = qm.OptionB,
                    OptionC = qm.OptionC,
                    OptionD = qm.OptionD,
                    OptionE = qm.OptionE,
                    CorrectOption = qm.CorrectOption,
                    AnswerKey = qm.AnswerKey,
                    IsActive = qm.IsActive,
                    IsFavorite = qm.IsFavorite,
                    CreatedByUserId = userId,
                    TopicIds = qm.TopicIds,
                    LearningOutcomeIds = qm.LearningOutcomeIds
                };

                await bankService.CreateAsync(model);
            }
            else
            {
                ErrorMessage = "Soru bankası servisi alınamadı.";
                return;
            }

            SuccessMessage = "Soru başarıyla oluşturuldu.";
            Question = new QuestionEditViewModel { QuestionNumber = 1 };
            QuestionSaved?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Kayıt hatası: " + ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }
}
