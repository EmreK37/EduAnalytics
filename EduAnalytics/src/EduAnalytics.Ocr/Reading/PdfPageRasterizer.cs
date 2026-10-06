using OpenCvSharp;
using PDFtoImage;
using SkiaSharp;

namespace EduAnalytics.Ocr.Reading;

/// <summary>
/// Tarayıcı çıktısı dosyaları OpenCV Mat'lerine çevirir.
/// PDF ise her sayfa ayrı görüntü olur; görüntü dosyaları tek sayfa sayılır.
/// </summary>
public static class PdfPageRasterizer
{
    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };

    public static bool IsSupported(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".pdf" || ImageExtensions.Contains(ext);
    }

    /// <summary>Dosyadaki her sayfayı gri Mat olarak döner. Çağıran Mat'leri dispose etmelidir.</summary>
    public static IEnumerable<Mat> LoadPages(string path, int dpi = 300)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext != ".pdf")
        {
            var single = Cv2.ImRead(path, ImreadModes.Grayscale);
            if (single.Empty())
                throw new InvalidOperationException($"Görüntü açılamadı: {Path.GetFileName(path)}");
            yield return single;
            yield break;
        }

        var pdfBytes = File.ReadAllBytes(path);
        foreach (SKBitmap bitmap in Conversion.ToImages(pdfBytes, options: new RenderOptions(Dpi: dpi)))
        {
            using (bitmap)
            {
                yield return ToGrayMat(bitmap);
            }
        }
    }

    /// <summary>SKBitmap piksellerini doğrudan kopyalayarak gri Mat üretir
    /// (PNG encode/decode turu 300 DPI A4'te sayfa başına gereksiz ~8 MP sıkıştırma demektir).</summary>
    private static Mat ToGrayMat(SKBitmap bitmap)
    {
        // PDFtoImage Bgra8888 üretir; farklı bir biçim gelirse önce ona kopyala.
        var source = bitmap.ColorType is SKColorType.Bgra8888 or SKColorType.Gray8
            ? bitmap
            : bitmap.Copy(SKColorType.Bgra8888)
              ?? throw new InvalidOperationException("PDF sayfası desteklenen piksel biçimine çevrilemedi.");
        try
        {
            // Wrapper SKBitmap'in belleğini sahiplenmez; kopya (Clone/CvtColor) bu blok içinde alınır.
            using var wrapper = Mat.FromPixelData(
                source.Height, source.Width,
                source.ColorType == SKColorType.Gray8 ? MatType.CV_8UC1 : MatType.CV_8UC4,
                source.GetPixels(), source.RowBytes);

            if (source.ColorType == SKColorType.Gray8)
                return wrapper.Clone();

            var gray = new Mat();
            Cv2.CvtColor(wrapper, gray, ColorConversionCodes.BGRA2GRAY);
            return gray;
        }
        finally
        {
            if (!ReferenceEquals(source, bitmap))
                source.Dispose();
        }
    }
}
