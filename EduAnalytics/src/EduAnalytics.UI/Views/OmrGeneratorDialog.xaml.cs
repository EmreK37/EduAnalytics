using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using EduAnalytics.Ocr;
using Microsoft.Win32;

namespace EduAnalytics.UI.Views;

public partial class OmrGeneratorDialog : Window
{
    private bool _isLoaded = false;
    private OpticalFormSpec _baseSpec;

    public OmrGeneratorDialog(OpticalFormSpec spec)
    {
        InitializeComponent();
        _baseSpec = spec;
        
        InstitutionTextBox.Text = spec.CourseName ?? "Kurum Adı";
        ExamNameTextBox.Text = spec.ExamTitle ?? "Sınav Adı";
        
        QuestionCountText.Text = spec.QuestionCount.ToString() + " (Sınavdan otomatik alındı)";
        
        if (spec.HasBookletSection)
        {
            BookletText.Text = "Var (" + string.Join(", ", spec.BookletCodes) + ")";
        }
        else
        {
            BookletText.Text = "Yok (Tek Kitapçık)";
        }
        
        _isLoaded = true;
        DrawPreview();
    }

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoaded)
        {
            DrawPreview();
        }
    }

    private OpticalFormSpec BuildCurrentSpec()
    {
        // Yalnızca Kurum Adı ve Sınav Adı değiştirilebilir, diğerleri baseSpec'ten gelir
        return new OpticalFormSpec
        {
            CourseName = InstitutionTextBox?.Text ?? "",
            ExamTitle = ExamNameTextBox?.Text ?? "",
            ExamDate = _baseSpec.ExamDate,
            QuestionNumbers = _baseSpec.QuestionNumbers,
            BookletCodes = _baseSpec.BookletCodes
        };
    }

    private void DrawPreview()
    {
        if (PaperPreview == null) return;
        PaperPreview.Children.Clear();

        var spec = BuildCurrentSpec();
        
        var blackBrush = Brushes.Black;
        var grayBrush = new SolidColorBrush(Color.FromRgb(156, 163, 175));
        var darkGrayBrush = new SolidColorBrush(Color.FromRgb(75, 85, 99));

        int markSize = 20;
        DrawRect(20, 20, markSize, markSize, blackBrush, null, 0);
        DrawRect(794 - 20 - markSize, 20, markSize, markSize, blackBrush, null, 0);
        DrawRect(20, 1123 - 20 - markSize, markSize, markSize, blackBrush, null, 0);
        DrawRect(794 - 20 - markSize, 1123 - 20 - markSize, markSize, markSize, blackBrush, null, 0);

        DrawText(spec.CourseName, 794 / 2, 60, 24, FontWeights.Bold, blackBrush, HorizontalAlignment.Center);
        DrawText(spec.ExamTitle, 794 / 2, 90, 18, FontWeights.Normal, darkGrayBrush, HorizontalAlignment.Center);
        DrawRect(100, 130, 594, 2, blackBrush);

        double currentY = 160;
        int studentNoLength = 12; 
        double startRightX = 794 - 100 - (studentNoLength * 20) - (spec.HasBookletSection ? 50 : 0);
        
        DrawText("Öğrenci Numarası", startRightX, currentY, 14, FontWeights.Bold, blackBrush, HorizontalAlignment.Left);
        currentY += 25;
        
        for (int col = 0; col < studentNoLength; col++)
        {
            DrawRect(startRightX + col * 20, currentY, 16, 16, Brushes.Transparent, blackBrush, 1);
            for (int row = 0; row < 10; row++)
            {
                DrawBubble(startRightX + col * 20 + 8, currentY + 25 + row * 20, row.ToString(), 12, grayBrush);
            }
        }

        if (spec.HasBookletSection)
        {
            double bookletX = startRightX + studentNoLength * 20 + 20;
            DrawText("Kitapçık", bookletX, 160, 14, FontWeights.Bold, blackBrush, HorizontalAlignment.Left);
            for (int i = 0; i < spec.BookletCodes.Count; i++)
            {
                DrawBubble(bookletX + 16, 185 + i * 25, spec.BookletCodes[i], 14, grayBrush);
            }
        }

        int cols = 4;
        int questionsPerCol = 25; 
        string[] options = { "A", "B", "C", "D", "E" };

        for (int i = 0; i < spec.QuestionCount; i++)
        {
            int col = i / questionsPerCol;
            int row = i % questionsPerCol;

            double x = 80 + col * 140;
            double y = 180 + row * 25;

            // Use the actual Question Number from the spec
            int qNum = spec.QuestionNumbers[i];

            DrawText($"{qNum}.", x, y - 6, 12, FontWeights.Bold, blackBrush, HorizontalAlignment.Right);

            for (int o = 0; o < 5; o++)
            {
                DrawBubble(x + 20 + o * 18, y, options[o], 12, grayBrush);
            }
        }

        for (int i = 0; i < questionsPerCol; i++)
        {
            DrawRect(20, 180 + i * 25 - 5, 10, 10, blackBrush);
        }
    }

    private void DrawRect(double x, double y, double width, double height, Brush fill, Brush stroke = null, double strokeThickness = 0)
    {
        var rect = new Rectangle { Width = width, Height = height, Fill = fill };
        if (stroke != null)
        {
            rect.Stroke = stroke;
            rect.StrokeThickness = strokeThickness;
        }
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        PaperPreview.Children.Add(rect);
    }

    private void DrawText(string text, double x, double y, double fontSize, FontWeight weight, Brush foreground, HorizontalAlignment alignment)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = weight,
            Foreground = foreground,
            TextAlignment = TextAlignment.Center
        };
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        
        if (alignment == HorizontalAlignment.Center) x -= tb.DesiredSize.Width / 2;
        else if (alignment == HorizontalAlignment.Right) x -= tb.DesiredSize.Width;

        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        PaperPreview.Children.Add(tb);
    }

    private void DrawBubble(double centerX, double centerY, string text, double size, Brush stroke)
    {
        var ellipse = new Ellipse
        {
            Width = size,
            Height = size,
            Stroke = stroke,
            StrokeThickness = 1,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(ellipse, centerX - size / 2);
        Canvas.SetTop(ellipse, centerY - size / 2);
        PaperPreview.Children.Add(ellipse);

        var tb = new TextBlock
        {
            Text = text,
            FontSize = size * 0.7,
            Foreground = stroke,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(tb, centerX - tb.DesiredSize.Width / 2);
        Canvas.SetTop(tb, centerY - tb.DesiredSize.Height / 2);
        PaperPreview.Children.Add(tb);
    }

    private void SavePdf_Click(object sender, RoutedEventArgs e)
    {
        var spec = BuildCurrentSpec();
        string safeName = string.IsNullOrWhiteSpace(spec.ExamTitle) ? "Sinav" : spec.ExamTitle.Replace(" ", "");
        var dialog = new SaveFileDialog
        {
            Filter = "PDF Dosyası|*.pdf",
            Title = "Optik Formu Kaydet",
            FileName = $"OptikForm_{safeName}.pdf"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var pdfBytes = OpticalFormPdfGenerator.Generate(spec);
                File.WriteAllBytes(dialog.FileName, pdfBytes);
                EduAnalytics.UI.Services.AppMessageBox.Show("Optik form PDF olarak başarıyla kaydedildi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                EduAnalytics.UI.Services.AppMessageBox.Show($"PDF üretilirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
