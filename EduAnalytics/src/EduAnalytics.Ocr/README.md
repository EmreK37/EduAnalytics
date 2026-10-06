# EduAnalytics.Ocr — Optik Okuma Modülü

Sınav cevap formlarını otomatik okuyan modül. İki teknoloji birlikte çalışır:

- **PaddleOCR** (Sdcb.PaddleOCR, in-process, Python gerektirmez): formun üst bölümündeki
  el yazısı **ad-soyad** ve **öğrenci numarası** alanlarını okur.
- **OMR** (OpenCvSharp ile optik işaret tanıma): çoktan seçmeli **cevap baloncuklarını**
  ve **kitapçık** işaretini piksel doluluk oranıyla okur. Baloncuk okuma bir OCR problemi
  değildir; bu yüzden bu bölümde PaddleOCR kullanılmaz.

## Akış

```
Optik Okuma ekranı (UI)
  1. Sınav seç  ──────────────►  OpticalFormPdfGenerator  →  yazdırılabilir PDF form
  2. Tarama yükle (PDF/JPG/PNG)
       └─► OpticalFormReader
             ├─ PdfPageRasterizer   : PDF sayfası → görüntü (300 DPI)
             ├─ PageNormalizer      : 4 köşe işareti → homografi → kanonik sayfa
             ├─ BubbleReader        : OMR — cevaplar + kitapçık (+ güven skoru)
             └─ PaddleTextReader    : PaddleOCR — ad-soyad + öğrenci no
  3. İnceleme ızgarası: düşük güvenli okumaları düzelt, öğrenciyi elle eşleştir
  4. Kaydet ──► IAnswerEntryService.SaveAsync  (kitapçık şık çözme + puanlama mevcut akışta)
```

## Kritik tasarım kuralı

`OpticalFormTemplate` **tek doğruluk kaynağıdır**: formu basan kod ile okuyan kod aynı
mm koordinatlarını kullanır. Form düzenini değiştirecekseniz **yalnızca** bu dosyayı
değiştirin; PDF üretici (SVG) ve okuyucu (piksel) otomatik uyumlu kalır.

Bu nedenle **taranan form mutlaka bu uygulamanın ürettiği form olmalıdır.**
Fotokopiyle çoğaltma sorun değildir (köşe işaretleri korunduğu sürece).

## NuGet paketleri ve sürüm notları

| Paket | Amaç |
|---|---|
| `Sdcb.PaddleOCR` + `Sdcb.PaddleOCR.Models.Local` | PaddleOCR .NET sarmalayıcı + yerel modeller (çevrimdışı çalışır) |
| `Sdcb.PaddleInference.runtime.win64.mkl` | Paddle çıkarım motoru (Windows x64, CPU) |
| `OpenCvSharp4` + `OpenCvSharp4.runtime.win` | Hizalama ve OMR |
| `PDFtoImage` | Taranmış PDF → görüntü |
| `QuestPDF` | Form PDF üretimi (UI zaten kullanıyor) |

İlk `dotnet restore` sırasında sürüm uyuşmazlığı çıkarsa (`csproj`'daki sürümler
yazıldığı tarihte günceldi) paketleri NuGet'teki son kararlı sürüme yükseltin.
Kod `LocalFullModels.LatinV3` modelini kullanır — Latin modeli Türkçe karakterleri
kapsar. `FullOcrModel.RecognizationModel` özellik adı Sdcb kütüphanesinin kendi
yazımıdır (yazım hatası değildir).

- Modeller pakete gömülüdür; çalışma anında internet **gerekmez**.
- Yalnızca **Windows x64**'te çalışır (uygulamanın geri kalanı gibi).
- PaddleOCR modelleri ilk okumada yüklenir (birkaç saniye); `OpticalFormReader`
  bu yüzden DI'da **singleton** kayıtlıdır.

## Form şablonu (v1)

- A4, tek sayfa, en fazla **100 soru** (4 sütun × 25 satır), her soruda **A–E** baloncukları.
- 4 köşede 6 mm dolu kare hizalama işareti → tarayıcı eğikliği homografiyle düzeltilir.
  Köşe işaretleri 180° dönüşe göre simetrik olduğundan **ters taranmış sayfa** ayrıca
  baloncuk çemberlerinin şablonla örtüşme skoruyla tespit edilir ve otomatik çevrilir
  (`BubbleReader.TemplateAlignmentScore`); satıra uyarı düşülür.
- Ad-soyad: tek satır kutu (serbest el yazısı, BÜYÜK HARF önerilir).
- Öğrenci no: 12 kutucuk, her kutuya tek rakam (kutucuklar tek tek OCR'lanır).
- Kitapçık: sınavda birden fazla kitapçık varsa A–E baloncukları basılır.
- Klasik (yazılı) sorular formda yer almaz; puanları Cevap Girişi ekranından girilir.

## Kitapçık ve soru sırası (ÖNEMLİ)

Sistem B/C/D kitapçıklarında yalnızca şıkları değil **soru sırasını da** karıştırır
(`ExamBookletService`). Bu yüzden:

- **Tek kitapçıklı sınavda** form, sınavdaki gerçek soru numaralarıyla basılır;
  satır i doğrudan ana sıradaki i. çoktan seçmeli soruya kaydedilir.
- **Çok kitapçıklı sınavda** form 1..N konum numaralarıyla basılır (forma açıklama
  notu eklenir). Kayıt sırasında satır i, öğrencinin işaretlediği kitapçığın
  `OrderInBooklet` sırasındaki i. çoktan seçmeli soruya eşlenir; bu nedenle çok
  kitapçıklı sınavda **kitapçık seçimi kaydetme için zorunludur**. Şık permütasyonu
  çözme (`DecodeStudentChoice`) mevcut kayıt servisinde, doğru soruya eşlenmiş
  QuestionId ile çalışır.

## Güven skoru ve eşikler

`BubbleReader` içindeki eşikler (300 DPI taramada makul varsayılanlar):

- `FilledThreshold = 0.40` — baloncuk içi mürekkep oranı bunun üstündeyse **dolu**.
- `WeakThreshold = 0.26` — bunun üstü ama dolu eşiğinin altı → **silik işaret**,
  incelemeye düşer.
- İki+ dolu baloncuk → **çift işaret**, cevap boş bırakılır ve uyarı üretilir.

Gerçek taramalarla pilot yaptıktan sonra bu iki sabiti ayarlayın (silgi izleri
yanlış "silik işaret" üretiyorsa `WeakThreshold`'u yükseltin).

## Öğrenci eşleştirme

`StudentMatcher`: OCR'lanan numara önce birebir, sonra ≤2 karakter hatayla (Levenshtein)
derse kayıtlı öğrencilerle eşleştirilir; numara okunamazsa ad benzerliği (bigram Dice)
denenir. Belirsizlikte eşleştirme **yapılmaz** — öğretmen ızgaradan elle seçer.
Yanlış öğrenciye not yazmaktansa boş bırakmak tercih edilmiştir.

## Test önerisi

1. Uygulamadan bir sınav için form üretin, yazdırın.
2. 5-10 formu farklı kalitede doldurun (silik işaret, çift işaret, boş, silgiyle
   düzeltilmiş) ve 300 DPI gri tonlamalı tarayın.
3. Optik Okuma ekranından yükleyin; uyarı üretilen alanların gerçekten sorunlu
   alanlar olduğunu doğrulayın.
4. Kaydettikten sonra Sınav Analizi ekranındaki sonuçları elle girilmiş bir
   kontrol öğrencisiyle karşılaştırın.
