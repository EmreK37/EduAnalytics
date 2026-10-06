using EduAnalytics.Core.Enums;

namespace EduAnalytics.Ocr.Models;

/// <summary>Tek bir taranmış optik formun okuma sonucu.</summary>
public class OpticalSheetResult
{
    /// <summary>Dört köşe hizalama işareti bulunup sayfa düzeltilebildi mi?
    /// false ise diğer alanlar boştur.</summary>
    public bool MarkersFound { get; set; }

    /// <summary>PaddleOCR'ın öğrenci no kutucuklarından okuduğu ham metin.</summary>
    public string RawStudentNumber { get; set; } = string.Empty;
    public double StudentNumberConfidence { get; set; }

    /// <summary>PaddleOCR'ın ad-soyad kutusundan okuduğu ham metin.</summary>
    public string RawStudentName { get; set; } = string.Empty;
    public double StudentNameConfidence { get; set; }

    /// <summary>İşaretlenen kitapçığın spec.BookletCodes içindeki index'i. null = okunamadı/işaretlenmedi.</summary>
    public int? BookletIndex { get; set; }
    public double BookletConfidence { get; set; }

    /// <summary>Formdaki satır sırasıyla (spec.QuestionNumbers sırası) cevaplar.</summary>
    public List<BubbleReadResult> Answers { get; } = new();

    /// <summary>İnsan tarafından incelenmesi gereken durumların Türkçe açıklamaları.</summary>
    public List<string> Warnings { get; } = new();
}

/// <summary>Tek bir sorunun baloncuk satırının okuma sonucu.</summary>
public class BubbleReadResult
{
    public int FormIndex { get; set; }
    public OptionLetter Option { get; set; } = OptionLetter.Empty;

    /// <summary>0–1 arası; işaret ile ikinci en dolu baloncuk arasındaki ayrıma dayanır.</summary>
    public double Confidence { get; set; }

    /// <summary>Birden fazla baloncuk dolu bulundu; Option=Empty bırakılır, öğretmen karar verir.</summary>
    public bool MultipleMarks { get; set; }

    /// <summary>Dolu eşiğinin altında ama boş sayılamayacak kadar karalanmış (silik/silinmiş işaret).</summary>
    public bool WeakMark { get; set; }
}
