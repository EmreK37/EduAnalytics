using EduAnalytics.Core.Enums;

namespace EduAnalytics.UI.ViewModels;

public sealed class QuestionTypeFilterOption
{
    public QuestionTypeFilterOption(string label, QuestionType? type)
    {
        Label = label;
        Type = type;
    }

    public string Label { get; }
    public QuestionType? Type { get; }
}
