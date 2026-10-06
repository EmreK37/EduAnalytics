using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EduAnalytics.Ocr;

/// <summary>
/// OpticalFormTemplate geometrisini birebir kullanarak yazdırılabilir optik form PDF'i üretir.
/// Çizim tek bir tam sayfa SVG olarak kurulur (1 SVG birimi = 1 mm); böylece baloncuk
/// koordinatları okuyucunun beklediği konumlarla garanti eşleşir.
/// </summary>
public static class OpticalFormPdfGenerator
{
    private const string InkColor = "#1a1a1a";
    private const string LightColor = "#9aa0a6";
    private const string FontFamily = "Lato, Arial, sans-serif";

    public static byte[] Generate(OpticalFormSpec spec)
    {
        if (spec.QuestionCount == 0)
            throw new ArgumentException("Formda basılacak soru yok.", nameof(spec));
        if (spec.QuestionCount > OpticalFormTemplate.MaxQuestions)
            throw new ArgumentException(
                $"Optik form en fazla {OpticalFormTemplate.MaxQuestions} soru destekler (istenen: {spec.QuestionCount}).",
                nameof(spec));
        if (spec.BookletCodes.Count > OpticalFormTemplate.MaxBooklets)
            throw new ArgumentException(
                $"Optik form en fazla {OpticalFormTemplate.MaxBooklets} kitapçık destekler.", nameof(spec));

        QuestPDF.Settings.License = LicenseType.Community;

        var svg = BuildSvg(spec);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.Content().Svg(SvgImage.FromText(svg));
            });
        }).GeneratePdf();
    }

    private static string BuildSvg(OpticalFormSpec spec)
    {
        var sb = new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" ")
          .Append($"width=\"{Mm(OpticalFormTemplate.PageWidthMm)}mm\" height=\"{Mm(OpticalFormTemplate.PageHeightMm)}mm\" ")
          .Append($"viewBox=\"0 0 {Mm(OpticalFormTemplate.PageWidthMm)} {Mm(OpticalFormTemplate.PageHeightMm)}\">");

        DrawMarkers(sb);
        DrawHeader(sb, spec);
        DrawStudentSection(sb, spec);
        DrawAnswerSection(sb, spec);

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static void DrawMarkers(StringBuilder sb)
    {
        var half = OpticalFormTemplate.MarkerSizeMm / 2;
        foreach (var c in OpticalFormTemplate.MarkerCenters)
        {
            sb.Append($"<rect x=\"{Mm(c.X - half)}\" y=\"{Mm(c.Y - half)}\" " +
                      $"width=\"{Mm(OpticalFormTemplate.MarkerSizeMm)}\" height=\"{Mm(OpticalFormTemplate.MarkerSizeMm)}\" fill=\"{InkColor}\"/>");
        }
    }

    private static void DrawHeader(StringBuilder sb, OpticalFormSpec spec)
    {
        Text(sb, 105, 22, "OPTİK CEVAP FORMU", 5, bold: true, anchor: "middle");
        Text(sb, 105, 29, Escape($"{spec.CourseName} — {spec.ExamTitle}"), 3.4, anchor: "middle");
        if (spec.ExamDate.HasValue)
            Text(sb, 105, 34.5, spec.ExamDate.Value.ToString("dd.MM.yyyy"), 2.8, anchor: "middle", color: LightColor);

        sb.Append($"<line x1=\"15\" y1=\"38\" x2=\"195\" y2=\"38\" stroke=\"{LightColor}\" stroke-width=\"0.25\"/>");
    }

    private static void DrawStudentSection(StringBuilder sb, OpticalFormSpec spec)
    {
        var name = OpticalFormTemplate.NameBox;
        Text(sb, name.X, name.Y - 1.5, "ADI SOYADI (büyük harflerle, kutunun içine yazınız)", 2.6, color: LightColor);
        Rect(sb, name, strokeWidth: 0.35);

        var num = OpticalFormTemplate.NumberRow;
        Text(sb, num.X, num.Y - 1.5, "ÖĞRENCİ NO (her kutuya tek rakam)", 2.6, color: LightColor);
        Rect(sb, num, strokeWidth: 0.35);
        for (var i = 1; i < OpticalFormTemplate.StudentNumberCells; i++)
        {
            var x = num.X + i * OpticalFormTemplate.NumberCellWidthMm;
            sb.Append($"<line x1=\"{Mm(x)}\" y1=\"{Mm(num.Y)}\" x2=\"{Mm(x)}\" y2=\"{Mm(num.Bottom)}\" " +
                      $"stroke=\"{LightColor}\" stroke-width=\"0.25\"/>");
        }

        if (!spec.HasBookletSection)
            return;

        Text(sb, OpticalFormTemplate.BookletBubbleFirstX - 3.5,
            OpticalFormTemplate.BookletBubbleY - 6, "KİTAPÇIK", 2.6, color: LightColor);
        for (var i = 0; i < spec.BookletCodes.Count; i++)
        {
            var c = OpticalFormTemplate.BookletBubbleCenter(i);
            Bubble(sb, c, spec.BookletCodes[i]);
        }
    }

    private static void DrawAnswerSection(StringBuilder sb, OpticalFormSpec spec)
    {
        var columnCount = (spec.QuestionCount + OpticalFormTemplate.QuestionsPerColumn - 1)
                          / OpticalFormTemplate.QuestionsPerColumn;

        // Çok kitapçıklı sınavda satır numaraları öğrencinin kendi kitapçığındaki soru sırasını izler.
        if (spec.HasBookletSection)
            Text(sb, OpticalFormTemplate.ColumnOriginsX[0], OpticalFormTemplate.AnswerFirstRowY - 9,
                "Cevaplar kitapçığınızdaki soru sırasına göre numaralandırılmıştır.", 2.4, color: LightColor);

        // Sütun başlıkları: A B C D E
        for (var col = 0; col < columnCount; col++)
        {
            for (var opt = 0; opt < OpticalFormTemplate.OptionCount; opt++)
            {
                var x = OpticalFormTemplate.ColumnOriginsX[col]
                        + OpticalFormTemplate.AnswerBubbleFirstOffsetX
                        + opt * OpticalFormTemplate.AnswerOptionPitch;
                Text(sb, x, OpticalFormTemplate.AnswerFirstRowY - 5, ((char)('A' + opt)).ToString(),
                    2.8, bold: true, anchor: "middle", color: LightColor);
            }
        }

        for (var i = 0; i < spec.QuestionCount; i++)
        {
            var (col, _) = OpticalFormTemplate.FormPosition(i);
            var firstBubble = OpticalFormTemplate.AnswerBubbleCenter(i, 0);

            Text(sb,
                OpticalFormTemplate.ColumnOriginsX[col] + OpticalFormTemplate.QuestionNumberOffsetX,
                firstBubble.Y + 1.1,
                spec.QuestionNumbers[i].ToString(), 2.8, anchor: "end");

            for (var opt = 0; opt < OpticalFormTemplate.OptionCount; opt++)
            {
                var c = OpticalFormTemplate.AnswerBubbleCenter(i, opt);
                Bubble(sb, c, ((char)('A' + opt)).ToString());
            }
        }
    }

    /// <summary>İçinde açık gri şık harfi olan boş baloncuk. Harf incedir; okuyucu tarafında
    /// erode adımı bu ince çizgileri sildiği için doluluk ölçümünü etkilemez.</summary>
    private static void Bubble(StringBuilder sb, MmPoint center, string letter)
    {
        sb.Append($"<circle cx=\"{Mm(center.X)}\" cy=\"{Mm(center.Y)}\" r=\"{Mm(OpticalFormTemplate.BubbleRadiusMm)}\" " +
                  $"fill=\"none\" stroke=\"{InkColor}\" stroke-width=\"0.25\"/>");
        Text(sb, center.X, center.Y + 0.8, Escape(letter), 2.2, anchor: "middle", color: LightColor);
    }

    private static void Rect(StringBuilder sb, MmRect r, double strokeWidth)
        => sb.Append($"<rect x=\"{Mm(r.X)}\" y=\"{Mm(r.Y)}\" width=\"{Mm(r.Width)}\" height=\"{Mm(r.Height)}\" " +
                     $"fill=\"none\" stroke=\"{InkColor}\" stroke-width=\"{Mm(strokeWidth)}\"/>");

    private static void Text(StringBuilder sb, double x, double y, string content, double sizeMm,
        bool bold = false, string anchor = "start", string color = InkColor)
    {
        sb.Append($"<text x=\"{Mm(x)}\" y=\"{Mm(y)}\" font-family=\"{FontFamily}\" font-size=\"{Mm(sizeMm)}\" " +
                  $"fill=\"{color}\" text-anchor=\"{anchor}\"{(bold ? " font-weight=\"bold\"" : "")}>{content}</text>");
    }

    private static string Mm(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Escape(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
