using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using EduAnalytics.Business.Dtos;

namespace EduAnalytics.UI.ViewModels;

public partial class SelectableLearningOutcome : ObservableObject
{
    private readonly ObservableCollection<LearningOutcomeDto> _selected;
    private bool _suppressSync;

    public LearningOutcomeDto Outcome { get; }

    public string Code => Outcome.Code;
    public string Name => Outcome.Name;

    [ObservableProperty]
    private bool _isSelected;

    public SelectableLearningOutcome(
        LearningOutcomeDto outcome,
        ObservableCollection<LearningOutcomeDto> selected)
    {
        Outcome = outcome;
        _selected = selected;
        _isSelected = selected.Any(s => s.Id == outcome.Id);
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (_suppressSync) return;
        if (value)
        {
            if (!_selected.Any(s => s.Id == Outcome.Id))
                _selected.Add(Outcome);
        }
        else
        {
            var existing = _selected.FirstOrDefault(s => s.Id == Outcome.Id);
            if (existing != null) _selected.Remove(existing);
        }
    }

    public void SyncFromExternal(bool selected)
    {
        _suppressSync = true;
        try { IsSelected = selected; }
        finally { _suppressSync = false; }
    }
}
