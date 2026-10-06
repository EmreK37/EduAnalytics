using EduAnalytics.Business.Dtos;
using EduAnalytics.UI.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace EduAnalytics.UI.ViewModels;

public partial class ExamAnalysisViewModel
{
    private static SKColor C(string hex) => SKColor.Parse(hex);

    private (SKColor primary, SKColor success, SKColor warning, SKColor danger,
             SKColor info, SKColor purple, SKColor cyan, SKColor muted) ChartColors()
    {
        var p = _themeService.GetChartPalette();
        return (C(p.Primary), C(p.Success), C(p.Warning), C(p.Danger),
                C(p.Info),    C(p.Purple),  C(p.Cyan),    C(p.Muted));
    }

    private void BuildLearningOutcomeChart(List<LearningOutcomePerformanceDto> outcomes)
    {
        var (_, _, _, _, info, _, _, _) = ChartColors();
        LearningOutcomeChartSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name = "Başarı %",
                Values = outcomes.Select(t => t.SuccessRate).ToArray(),
                Fill = new SolidColorPaint(info)
            }
        };

        LearningOutcomeChartXAxes = new[]
        {
            new Axis
            {
                Labels = outcomes.Select(t => t.OutcomeName.Split(' ').FirstOrDefault() ?? "ÖÇ").ToArray(),
                LabelsRotation = 0
            }
        };

        LearningOutcomeChartYAxes = new[]
        {
            new Axis { Name = "Başarı %", MinLimit = 0, MaxLimit = 100 }
        };
    }

    private void BuildQuestionChart(List<QuestionAnalysisDto> questions)
    {
        var (_, success, _, _, _, _, _, muted) = ChartColors();
        QuestionSuccessSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name = "Başarı %",
                Values = questions.Select(q => q.SuccessRate).ToArray(),
                Fill = new SolidColorPaint(success),
                Stroke = null,
                MaxBarWidth = 56
            }
        };

        QuestionXAxes = new[]
        {
            new Axis
            {
                Labels = questions.Select(q => $"S{q.QuestionNumber}").ToArray(),
                SeparatorsPaint = null,
                LabelsPaint = new SolidColorPaint(muted),
                TextSize = 13
            }
        };

        QuestionYAxes = new[]
        {
            new Axis
            {
                Name = "Başarı %",
                MinLimit = 0,
                MaxLimit = 100,
                SeparatorsPaint = new SolidColorPaint(muted.WithAlpha(35)) { StrokeThickness = 1 },
                LabelsPaint = new SolidColorPaint(muted),
                TextSize = 13
            }
        };
    }

    private void BuildScoreDistributionHistogram(List<StudentPerformanceDto> students, decimal maxScore)
    {
        if (students.Count == 0 || maxScore <= 0) return;

        var (_, _, _, _, _, purple, _, _) = ChartColors();

        int[] bins = new int[5];
        foreach (var s in students)
        {
            double pct = (double)(s.TotalScore / maxScore) * 100;
            if (pct < 20) bins[0]++;
            else if (pct < 40) bins[1]++;
            else if (pct < 60) bins[2]++;
            else if (pct < 80) bins[3]++;
            else bins[4]++;
        }

        ScoreDistributionSeries = new ISeries[]
        {
            new ColumnSeries<int>
            {
                Name = "Öğrenci Sayısı",
                Values = bins,
                Fill = new SolidColorPaint(purple)
            }
        };

        ScoreDistributionXAxes = new[]
        {
            new Axis { Labels = new[] { "0-20%", "20-40%", "40-60%", "60-80%", "80-100%" } }
        };
    }

    private void BuildSuccessRateDoughnut(double avgSuccess, IReadOnlyCollection<ItemAnalysisDto>? items = null)
    {
        var (_, success, _, danger, _, _, _, muted) = ChartColors();
        var correct = items?.Sum(i => i.CorrectCount) ?? 0;
        var wrong = items?.Sum(i => i.WrongCount) ?? 0;
        var empty = items?.Sum(i => i.EmptyCount) ?? 0;
        var total = correct + wrong + empty;

        if (total == 0)
        {
            correct = (int)Math.Round(avgSuccess);
            wrong = Math.Max(0, 100 - correct);
            empty = 0;
            total = correct + wrong;
        }

        CorrectAnswerCount = correct;
        WrongAnswerCount = wrong;
        EmptyAnswerCount = empty;
        TotalAnswerCount = total;
        OnPropertyChanged(nameof(CorrectAnswerRate));
        OnPropertyChanged(nameof(WrongAnswerRate));
        OnPropertyChanged(nameof(EmptyAnswerRate));

        SuccessRateDoughnutSeries = new ISeries[]
        {
            new PieSeries<double>
            {
                Values = new double[] { correct },
                Name = "Doğru",
                Fill = new SolidColorPaint(success),
                Stroke = null,
                InnerRadius = 58
            },
            new PieSeries<double>
            {
                Values = new double[] { wrong },
                Name = "Yanlış",
                Fill = new SolidColorPaint(danger),
                Stroke = null,
                InnerRadius = 58
            },
            new PieSeries<double>
            {
                Values = new double[] { empty },
                Name = "Boş",
                Fill = new SolidColorPaint(muted.WithAlpha(105)),
                Stroke = null,
                InnerRadius = 58
            }
        };
    }

    private void BuildRadarChart(List<LearningOutcomePerformanceDto> outcomes)
    {
        if (outcomes.Count == 0) return;

        var (_, _, _, _, info, _, _, _) = ChartColors();
        RadarSeries = new ISeries[]
        {
            new PolarLineSeries<double>
            {
                Values = outcomes.Select(t => t.SuccessRate).ToArray(),
                Name = "Sınıf Ortalaması",
                LineSmoothness = 0,
                GeometrySize = 10,
                Fill   = new SolidColorPaint(info.WithAlpha(90)),
                Stroke = new SolidColorPaint(info) { StrokeThickness = 2 }
            }
        };

        RadarAxes = new PolarAxis[]
        {
            new PolarAxis
            {
                Labels = outcomes.Select(t => t.OutcomeName.Split(' ').FirstOrDefault() ?? "ÖÇ").ToArray()
            }
        };
    }

    private void BuildDifficultyChart(List<ItemAnalysisDto> items)
    {
        if (items.Count == 0) return;

        var (_, _, warning, _, _, _, _, _) = ChartColors();
        DifficultyChartSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name = "Güçlük (p)",
                Values = items.Select(i => i.DifficultyIndex).ToArray(),
                Fill = new SolidColorPaint(warning)
            }
        };

        DifficultyChartXAxes = new[]
        {
            new Axis { Labels = items.Select(i => $"S{i.QuestionNumber}").ToArray() }
        };

        DifficultyChartYAxes = new[]
        {
            new Axis { Name = "Güçlük indeksi", MinLimit = 0, MaxLimit = 1 }
        };
    }

    private void BuildDiscriminationChart(List<ItemAnalysisDto> items)
    {
        if (items.Count == 0) return;

        var (_, _, _, _, _, _, cyan, _) = ChartColors();
        DiscriminationChartSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name = "Ayırt edicilik (D)",
                Values = items.Select(i => i.DiscriminationIndex).ToArray(),
                Fill = new SolidColorPaint(cyan)
            }
        };

        DiscriminationChartXAxes = new[]
        {
            new Axis { Labels = items.Select(i => $"S{i.QuestionNumber}").ToArray() }
        };

        DiscriminationChartYAxes = new[]
        {
            new Axis { Name = "Ayırt edicilik", MinLimit = -1, MaxLimit = 1 }
        };
    }

    private void BuildUpperLowerChart(List<ItemAnalysisDto> items)
    {
        if (items.Count == 0) return;

        var (_, success, _, danger, _, _, _, _) = ChartColors();
        UpperLowerSeries = new ISeries[]
        {
            new ColumnSeries<int>
            {
                Name = "Üst %27 (Doğru)",
                Values = items.Select(i => i.UpperGroupCorrect).ToArray(),
                Fill = new SolidColorPaint(success)
            },
            new ColumnSeries<int>
            {
                Name = "Alt %27 (Doğru)",
                Values = items.Select(i => i.LowerGroupCorrect).ToArray(),
                Fill = new SolidColorPaint(danger)
            }
        };

        UpperLowerXAxes = new[]
        {
            new Axis { Labels = items.Select(i => $"S{i.QuestionNumber}").ToArray() }
        };

        UpperLowerYAxes = new[]
        {
            new Axis { Name = "Doğru sayan öğrenci", MinLimit = 0 }
        };
    }

    private void BuildAnswerDistributionChart(List<ItemAnalysisDto> items)
    {
        if (items.Count == 0) return;

        var (_, success, _, danger, _, _, _, muted) = ChartColors();
        AnswerDistributionSeries = new ISeries[]
        {
            new ColumnSeries<int>
            {
                Name = "Doğru",
                Values = items.Select(i => i.CorrectCount).ToArray(),
                Fill = new SolidColorPaint(success)
            },
            new ColumnSeries<int>
            {
                Name = "Yanlış",
                Values = items.Select(i => i.WrongCount).ToArray(),
                Fill = new SolidColorPaint(danger)
            },
            new ColumnSeries<int>
            {
                Name = "Boş",
                Values = items.Select(i => i.EmptyCount).ToArray(),
                Fill = new SolidColorPaint(muted)
            }
        };

        AnswerDistributionXAxes = new[]
        {
            new Axis { Labels = items.Select(i => $"S{i.QuestionNumber}").ToArray() }
        };

        AnswerDistributionYAxes = new[]
        {
            new Axis { Name = "Öğrenci sayısı", MinLimit = 0 }
        };
    }
}
