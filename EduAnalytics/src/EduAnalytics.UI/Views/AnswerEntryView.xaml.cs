using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using EduAnalytics.Core.Enums;
using EduAnalytics.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace EduAnalytics.UI.Views;

public partial class AnswerEntryView : UserControl
{
    public AnswerEntryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AnswerEntryViewModel old)
        {
            old.QuestionsLoaded -= BuildColumns;
            old.OpenRubricRequested -= OnOpenRubricRequested;
        }

        if (e.NewValue is AnswerEntryViewModel vm)
        {
            vm.QuestionsLoaded += BuildColumns;
            vm.OpenRubricRequested += OnOpenRubricRequested;
            if (vm.Questions.Any())
                BuildColumns();
        }
    }

    private async void OnOpenRubricRequested(int questionId, int studentId)
    {
        if (DataContext is not AnswerEntryViewModel vm) return;

        var question = vm.Questions.FirstOrDefault(q => q.QuestionId == questionId);
        var student = vm.StudentRows.FirstOrDefault(s => s.StudentId == studentId);
        if (question == null || student == null) return;

        var rubricVm = App.Services.GetRequiredService<RubricGradeDialogViewModel>();
        await rubricVm.LoadAsync(
            vm.ExamId, questionId, studentId,
            $"Soru {question.QuestionNumber}: {question.QuestionTextPreview} ({question.MaxPoints} puan)",
            $"{student.StudentNumber} — {student.FullName}");

        var dialog = new RubricGradeDialog { Owner = Window.GetWindow(this) };
        dialog.Bind(rubricVm);

        if (dialog.ShowDialog() == true)
        {
            // Kaydedilen toplam puanı ilgili hücreye anında yansıt — listede de güncel görünsün.
            var cell = student.Cells.FirstOrDefault(c => c.QuestionId == questionId);
            if (cell != null)
                cell.Score = rubricVm.TotalScore;

            await vm.ReloadAsync();
        }
    }

    /// <summary>
    /// Sorular yüklendikten sonra DataGrid kolonlarını dinamik olarak oluşturur.
    /// Test sorusu → ComboBox (A/B/C/D/Boş). Klasik soru → TextBox (numerik).
    /// </summary>
    private void BuildColumns()
    {
        if (DataContext is not AnswerEntryViewModel vm) return;

        AnswersGrid.Columns.Clear();

        // Öğrenci bilgi sütunları (sabit / donmuş)
        AnswersGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Numara",
            Binding = new Binding("StudentNumber"),
            IsReadOnly = true,
            Width = new DataGridLength(90),
            CellStyle = MakeCellStyle(bold: true, backgroundResourceKey: "CardSubtleBgBrush")
        });

        AnswersGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Öğrenci",
            Binding = new Binding("FullName"),
            IsReadOnly = true,
            Width = new DataGridLength(180),
            CellStyle = MakeCellStyle(bold: true, backgroundResourceKey: "CardSubtleBgBrush")
        });

        // Kitapçık sütunu (sınavda 2+ kitapçık varsa kullanışlı; tek kitapçıkta da görünür)
        if (vm.AvailableBookletCodes.Count > 0)
        {
            var bookletCol = new DataGridComboBoxColumn
            {
                Header = "Kitapçık",
                ItemsSource = vm.AvailableBookletCodes,
                SelectedItemBinding = new Binding("BookletCode")
                {
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                },
                Width = new DataGridLength(85),
                CellStyle = MakeCellStyle(bold: true, backgroundResourceKey: "WarningSubtleBgBrush")
            };
            AnswersGrid.Columns.Add(bookletCol);
        }

        // Soru sütunları
        for (int i = 0; i < vm.Questions.Count; i++)
        {
            var q = vm.Questions[i];
            var header = q.Type == QuestionType.MultipleChoice
                ? $"S{q.QuestionNumber}\n(Test {q.MaxPoints:0.#}p)"
                : $"S{q.QuestionNumber}\n(Klasik {q.MaxPoints:0.#}p)";

            DataGridColumn column;

            if (q.Type == QuestionType.MultipleChoice)
            {
                var comboCol = new DataGridComboBoxColumn
                {
                    Header = header,
                    ItemsSource = vm.OptionLetters,
                    SelectedItemBinding = new Binding($"Cells[{i}].SelectedOption")
                    {
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    },
                    Width = new DataGridLength(70)
                };
                column = comboCol;
            }
            else
            {
                // Klasik: TextBox + not detay butonu (rubric kriter puanlaması)
                var template = new DataTemplate();
                var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
                stackFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                stackFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                stackFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

                var tbFactory = new FrameworkElementFactory(typeof(TextBox));
                tbFactory.SetBinding(TextBox.TextProperty, new Binding($"Cells[{i}].Score")
                {
                    StringFormat = "N1",
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    TargetNullValue = string.Empty
                });
                tbFactory.SetValue(FrameworkElement.WidthProperty, 60.0);
                tbFactory.SetValue(FrameworkElement.HeightProperty, 26.0);
                tbFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                tbFactory.SetValue(Control.PaddingProperty, new Thickness(4, 2, 4, 2));
                tbFactory.SetValue(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center);
                tbFactory.SetValue(TextBox.TextAlignmentProperty, TextAlignment.Center);
                tbFactory.SetResourceReference(Control.BackgroundProperty, "WarningSubtleBgBrush");

                var btnFactory = new FrameworkElementFactory(typeof(Button));
                btnFactory.SetValue(Button.ContentProperty, "Not");
                btnFactory.SetValue(Button.ToolTipProperty, "Detaylı (kriter-bazlı) puanla");
                btnFactory.SetValue(FrameworkElement.WidthProperty, 26.0);
                btnFactory.SetValue(FrameworkElement.HeightProperty, 26.0);
                btnFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                btnFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 0, 0));
                btnFactory.SetValue(Control.PaddingProperty, new Thickness(0));
                btnFactory.SetValue(Control.BorderThicknessProperty, new Thickness(1));
                btnFactory.SetValue(Control.FontSizeProperty, 11.0);
                btnFactory.SetValue(FrameworkElement.CursorProperty, System.Windows.Input.Cursors.Hand);
                btnFactory.SetBinding(Button.CommandProperty, new Binding("DataContext.OpenRubricDialogCommand")
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
                    {
                        AncestorType = typeof(UserControl)
                    }
                });
                btnFactory.SetBinding(Button.CommandParameterProperty, new Binding($"Cells[{i}]"));

                stackFactory.AppendChild(tbFactory);
                stackFactory.AppendChild(btnFactory);
                template.VisualTree = stackFactory;

                column = new DataGridTemplateColumn
                {
                    Header = header,
                    CellTemplate = template,
                    Width = new DataGridLength(120)
                };
            }

            AnswersGrid.Columns.Add(column);
        }
    }

    private static Style MakeCellStyle(bool bold = false, string? backgroundResourceKey = null)
    {
        var style = new Style(typeof(DataGridCell));
        if (bold)
            style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        if (backgroundResourceKey != null)
            style.Setters.Add(new Setter(Control.BackgroundProperty,
                new DynamicResourceExtension(backgroundResourceKey)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
        return style;
    }

    private static Style MakeTextElementStyle(string? backgroundResourceKey = null)
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(8, 4, 8, 4)));
        style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        if (backgroundResourceKey != null)
            style.Setters.Add(new Setter(TextBlock.BackgroundProperty,
                new DynamicResourceExtension(backgroundResourceKey)));
        return style;
    }

    private static Style MakeEditingStyle()
    {
        var style = new Style(typeof(TextBox));
        style.Setters.Add(new Setter(TextBox.TextAlignmentProperty, TextAlignment.Center));
        style.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(6, 2, 6, 2)));
        return style;
    }
}
