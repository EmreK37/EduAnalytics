using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EduAnalytics.UI.ViewModels;

public partial class ExamAnalysisViewModel
{
    [RelayCommand]
    private async Task ExportReportAsync()
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Filter = "PDF Dosyası (*.pdf)|*.pdf",
                Title = "Analiz Raporunu Dışa Aktar",
                FileName = $"{Summary?.ExamTitle ?? "Analiz"}_Raporu.pdf"
            };

            if (sfd.ShowDialog() == true)
            {
                IsLoading = true;

                QuestPDF.Settings.License = LicenseType.Community;

                await Task.Run(() =>
                {
                    Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Margin(2, Unit.Centimetre);
                            page.PageColor(Colors.White);
                            page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Arial));

                            page.Header().Element(ComposeHeader);
                            page.Content().Element(ComposeContent);
                            page.Footer().Element(ComposeFooter);
                        });
                    })
                    .GeneratePdf(sfd.FileName);
                });

                ErrorMessage = null;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"PDF Dışa Aktarım Hatası: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(Summary?.ExamTitle ?? "Sınav Analiz Raporu").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().Text(Summary?.CourseName ?? "Ders Adı Yok").FontSize(14).FontColor(Colors.Grey.Medium);
            });
            row.ConstantItem(100).AlignRight().Text($"{DateTime.Now:dd.MM.yyyy}").FontSize(10);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(1, Unit.Centimetre).Column(column =>
        {
            column.Spacing(20);

            column.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Genel Sınav Özeti").FontSize(14).SemiBold();

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Cell().Text("Öğrenci Sayısı").SemiBold();
                table.Cell().Text("Soru Sayısı").SemiBold();
                table.Cell().Text("Genel Başarı").SemiBold();
                table.Cell().Text("Ortalama Puan").SemiBold();

                table.Cell().Text(Summary?.TotalStudents.ToString() ?? "-");
                table.Cell().Text(Summary?.TotalQuestions.ToString() ?? "-");
                table.Cell().Text($"%{Summary?.AverageSuccessRate:N1}");
                table.Cell().Text($"{Summary?.AverageScore:N2} / {Summary?.MaxPossibleScore:N0}");
            });

            if (Statistics != null)
            {
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5)
                    .Text("Sınav Betimsel İstatistikleri").FontSize(14).SemiBold();

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    void Row(string a, string av, string b, string bv)
                    {
                        table.Cell().Text(a).SemiBold();
                        table.Cell().Text(av);
                        table.Cell().Text(b).SemiBold();
                        table.Cell().Text(bv);
                    }

                    Row("Aritmetik Ortalama", $"{Statistics.Mean:N2}",
                        "Ortanca",            $"{Statistics.Median:N2}");
                    Row("Tepe Değer",         $"{Statistics.Mode:N2}",
                        "Ranj",               $"{Statistics.Range:N2}");
                    Row("Q1",                 $"{Statistics.Q1:N2}",
                        "Q3",                 $"{Statistics.Q3:N2}");
                    Row("IQR (Q3 − Q1)",      $"{Statistics.InterquartileRange:N2}",
                        "Çeyrek Kayma",       $"{Statistics.SemiInterquartileRange:N2}");
                    Row("ÇKDK",               $"{Statistics.QuartileCoefficient:N3}",
                        "Standart Sapma",     $"{Statistics.StandardDeviation:N2}");
                    Row("Ort.dan Mutlak Kayma (MAD)", $"{Statistics.MeanAbsoluteDeviation:N2}",
                        "Ortc.dan Mut. Kayma (MedAD)", $"{Statistics.MedianAbsoluteDeviation:N2}");
                    Row("Standart Hata",      $"{Statistics.StandardError:N2}",
                        "BDK (CV %)",         $"{Statistics.CoefficientOfVariation:N2}");
                    Row("Çarpıklık (Skewness)", $"{Statistics.Skewness:N3}",
                        "Basıklık (Kurtosis)",  $"{Statistics.Kurtosis:N3}");
                    Row("Cronbach α",         $"{Statistics.CronbachAlpha:N3} ({Statistics.CronbachAlphaInterpretation})",
                        "Geçme Notu",         $"{Statistics.PassingScore:N2} (eşik: %{Statistics.AcceptabilityIndex:N0})");
                    Row("Geçen / Kalan",      $"{Statistics.PassedStudentCount} / {Statistics.FailedStudentCount}",
                        "Geçme Oranı",        $"%{Statistics.PassRate:N1}");
                });
            }

            if (ItemAnalysis != null && ItemAnalysis.Count > 0)
            {
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5)
                    .Text("Madde Analizi (Klasik Test Geliştirme)").FontSize(14).SemiBold();

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(40);
                        columns.ConstantColumn(40);
                        columns.ConstantColumn(45);
                        columns.ConstantColumn(50);
                        columns.RelativeColumn();
                        columns.ConstantColumn(50);
                        columns.RelativeColumn();
                        columns.ConstantColumn(55);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Soru").SemiBold();
                        header.Cell().Text("N").SemiBold();
                        header.Cell().Text("Doğru").SemiBold();
                        header.Cell().Text("p").SemiBold();
                        header.Cell().Text("Güçlük").SemiBold();
                        header.Cell().Text("D").SemiBold();
                        header.Cell().Text("Ayırt Edicilik").SemiBold();
                        header.Cell().Text("r_jx").SemiBold();
                    });

                    foreach (var item in ItemAnalysis)
                    {
                        table.Cell().Text($"S{item.QuestionNumber}");
                        table.Cell().Text(item.TotalStudents.ToString());
                        table.Cell().Text(item.CorrectCount.ToString());
                        table.Cell().Text($"{item.DifficultyIndex:N3}");
                        table.Cell().Text(item.DifficultyCategory);
                        table.Cell().Text($"{item.DiscriminationIndex:N3}");
                        table.Cell().Text(item.DiscriminationCategory);
                        table.Cell().Text($"{item.ItemReliabilityIndex:N3}");
                    }
                });
            }

            if (StrongDistractors != null && StrongDistractors.Count > 0)
            {
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("En Güçlü Çeldiriciler").FontSize(14).SemiBold().FontColor(Colors.Red.Darken2);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Soru").SemiBold();
                        header.Cell().Text("Doğru").SemiBold();
                        header.Cell().Text("Çeldirici").SemiBold();
                        header.Cell().Text("Yanılan %").SemiBold();
                    });

                    foreach (var dist in StrongDistractors)
                    {
                        table.Cell().Text($"Soru {dist.QuestionNumber}");
                        table.Cell().Text(dist.CorrectOption);
                        table.Cell().Text(dist.StrongestDistractorOption).FontColor(Colors.Red.Medium);
                        table.Cell().Text($"%{dist.StrongestDistractorRate:N1}");
                    }
                });
            }

            if (LearningOutcomes != null && LearningOutcomes.Count > 0)
            {
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Öğrenim Çıktısı Başarı Dağılımı").FontSize(14).SemiBold();

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Öğrenim Çıktısı").SemiBold();
                        header.Cell().Text("Açıklama").SemiBold();
                        header.Cell().Text("Başarı Oranı").SemiBold();
                    });

                    foreach (var outcome in LearningOutcomes)
                    {
                        table.Cell().Text(outcome.OutcomeName);
                        table.Cell().Text(outcome.Description ?? "-");
                        table.Cell().Text($"%{outcome.SuccessRate:N1} - {outcome.PerformanceLevel}");
                    }
                });
            }
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(x =>
        {
            x.Span("Sayfa ");
            x.CurrentPageNumber();
            x.Span(" / ");
            x.TotalPages();
        });
    }
}
