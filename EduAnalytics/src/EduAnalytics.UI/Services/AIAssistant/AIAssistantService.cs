using System.Text;
using EduAnalytics.UI.Services;

namespace EduAnalytics.UI.Services.AIAssistant;

public interface IAIAssistantService
{
    Task SendAsync(
        IReadOnlyList<ChatMessage> messages,
        string? activeViewContext,
        StreamDeltaHandler onDelta,
        CancellationToken ct);

    Task<IReadOnlyList<string>> GenerateSuggestedPromptsAsync(
        string? activeViewContext,
        string? lastQuestion,
        string? lastAnswer,
        CancellationToken ct);

    bool HasConfiguredKey { get; }
    AIProviderKind ActiveProvider { get; }
    string ActiveModel { get; }
    string LogFilePath { get; }

    Task TestConfigurationAsync(
        AIProviderKind provider,
        string model,
        string? apiKeyOverride,
        CancellationToken ct);
}

public sealed class AIAssistantService : IAIAssistantService
{
    private readonly IAISettingsService _settings;
    private readonly IAppLogService _log;
    private readonly Dictionary<AIProviderKind, IAIProvider> _providers;

    public AIAssistantService(
        IAISettingsService settings,
        IEnumerable<IAIProvider> providers,
        IAppLogService log)
    {
        _settings = settings;
        _log = log;
        _providers = providers.ToDictionary(p => p.Kind);
    }

    public AIProviderKind ActiveProvider => _settings.Current.ActiveProvider;
    public string ActiveModel => _settings.Current.ActiveModel;
    public string LogFilePath => _log.CurrentLogFilePath;

    public bool HasConfiguredKey =>
        !string.IsNullOrWhiteSpace(_settings.GetApiKey(_settings.Current.ActiveProvider));

    public async Task SendAsync(
        IReadOnlyList<ChatMessage> messages,
        string? activeViewContext,
        StreamDeltaHandler onDelta,
        CancellationToken ct)
    {
        var providerKind = _settings.Current.ActiveProvider;
        if (!_providers.TryGetValue(providerKind, out var provider))
            throw new AIException($"Provider bulunamadı: {providerKind}");

        var apiKey = _settings.GetApiKey(providerKind);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AIException($"{providerKind} API anahtarı tanımlı değil. Sağ panel ayarlarından ekleyin.");

        _log.Info("AI.Send",
            $"Starting request. Provider={providerKind}, Model={_settings.Current.ActiveModel}, Messages={messages.Count}, ContextChars={activeViewContext?.Length ?? 0}");

        var request = new AIRequest
        {
            Model = _settings.Current.ActiveModel,
            Messages = messages,
            SystemPrompt = BuildSystemPrompt(activeViewContext),
            MaxTokens = 1500
        };

        try
        {
            await provider.StreamCompletionAsync(request, apiKey, onDelta, ct);
            _log.Info("AI.Send", $"Request completed. Provider={providerKind}, Model={request.Model}");
        }
        catch (Exception ex)
        {
            _log.Error("AI.Send",
                $"Request failed. Provider={providerKind}, Model={request.Model}",
                ex);
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> GenerateSuggestedPromptsAsync(
        string? activeViewContext,
        string? lastQuestion,
        string? lastAnswer,
        CancellationToken ct)
    {
        var providerKind = _settings.Current.ActiveProvider;
        if (!_providers.TryGetValue(providerKind, out var provider))
            throw new AIException($"Provider bulunamadı: {providerKind}");

        var apiKey = _settings.GetApiKey(providerKind);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AIException($"{providerKind} API anahtarı tanımlı değil. Sağ panel ayarlarından ekleyin.");

        var output = new StringBuilder();
        var request = new AIRequest
        {
            Model = _settings.Current.ActiveModel,
            SystemPrompt = """
                Sen EduAnalytics uygulaması için öneri soru üreten kısa bir yardımcı modelsin.
                Yalnızca kullanıcının tıklayabileceği 3 Türkçe soru üret.
                Her soru aktif ekran bağlamına veya son verilen cevaba doğrudan bağlı olsun.
                Genel, hazır, ekranla ilgisiz soru yazma.
                Yanıt formatı: sadece 3 satır. Numara, madde işareti, açıklama veya başlık kullanma.
                """,
            Messages = new[]
            {
                new ChatMessage
                {
                    Role = ChatRole.User,
                    Content = BuildSuggestionPrompt(activeViewContext, lastQuestion, lastAnswer)
                }
            },
            MaxTokens = 320
        };

        _log.Info("AI.Suggestions",
            $"Generating suggestions. Provider={providerKind}, Model={request.Model}, ContextChars={activeViewContext?.Length ?? 0}, AnswerChars={lastAnswer?.Length ?? 0}");

        await provider.StreamCompletionAsync(request, apiKey, delta => output.Append(delta), ct);
        return ParseSuggestedPrompts(output.ToString());
    }

    public async Task TestConfigurationAsync(
        AIProviderKind providerKind,
        string model,
        string? apiKeyOverride,
        CancellationToken ct)
    {
        if (!_providers.TryGetValue(providerKind, out var provider))
            throw new AIException($"Provider bulunamadı: {providerKind}");

        var apiKey = string.IsNullOrWhiteSpace(apiKeyOverride)
            ? _settings.GetApiKey(providerKind)
            : apiKeyOverride.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AIException($"{providerKind} API anahtarı tanımlı değil. Test için API anahtarı girin.");

        if (string.IsNullOrWhiteSpace(model))
            throw new AIException("Test için model seçin veya model id girin.");

        _log.Info("AI.Test", $"Testing configuration. Provider={providerKind}, Model={model}");

        var output = new StringBuilder();
        var request = new AIRequest
        {
            Model = model.Trim(),
            SystemPrompt = "Sen sadece bağlantı testi yapan kısa bir asistansın.",
            Messages = new[]
            {
                new ChatMessage
                {
                    Role = ChatRole.User,
                    Content = "Bağlantı testi. Yanıt olarak kısa biçimde OK yaz."
                }
            },
            MaxTokens = 256
        };

        try
        {
            await provider.StreamCompletionAsync(request, apiKey, delta => output.Append(delta), ct);

            if (string.IsNullOrWhiteSpace(output.ToString()))
                throw new AIException($"{providerKind} bağlantısı kuruldu ancak model boş yanıt döndürdü.");

            _log.Info("AI.Test",
                $"Configuration test succeeded. Provider={providerKind}, Model={request.Model}, ResponseChars={output.Length}");
        }
        catch (Exception ex)
        {
            _log.Error("AI.Test",
                $"Configuration test failed. Provider={providerKind}, Model={request.Model}",
                ex);
            throw;
        }
    }

    private static string BuildSystemPrompt(string? activeViewContext)
    {
        var basePrompt = """
            Sen EduAnalytics adlı eğitim ölçme ve değerlendirme uygulamasının yapay zeka asistanısın.
            Kullanıcı bir akademisyen, eğitmen veya program yöneticisi olabilir.

            Görevin:
            - Uygulamanın nasıl kullanılacağını adım adım anlatmak.
            - Dashboard, sınav analizi, soru bankası, bankadan sınav oluşturma, sınav yönetimi, öğrenim çıktıları, program çıktıları, PÇ-ÖÇ eşleştirme, PÇ başarı raporu ve öğrenci yönetimi hakkında rehberlik etmek.
            - Aktif ekranda verilen analiz bağlamını kullanarak sınav kalitesi, güvenilirlik, madde analizi, öğrenim çıktısı başarısı ve program çıktısı katkısını yorumlamak.
            - Kullanıcı ne yapacağını sorarsa uygulanabilir kısa adımlar vermek.
            - Kullanıcı veri yorumu isterse ölçme-değerlendirme terminolojisini doğru kullanmak: Cronbach alfa, madde güçlüğü, ayırt edicilik, öğrenim çıktısı, program çıktısı, sınav dengesi.
            - Sayısal eşiklere dikkat etmek: alfa >= 0.70 genelde kabul edilebilir, düşük ayırt edicilik revizyon sinyali olabilir, çok kolay/çok zor maddeler sınav dengesini etkileyebilir.
            - Veri yetersizse tahmin yapma; hangi ekran, veri veya rapor gerektiğini söyle.
            - Öğrenci kimliği, API anahtarı veya gizli veri isteme.

            Uygulama rehberi:
            - Dashboard: genel sınavlar, özet metrikler ve sınav performansına geçiş için kullanılır.
            - Sınav Yönetimi: mevcut sınavları listeleme, analiz açma ve yönetim işlemleri içindir.
            - Soru Bankası: soruları filtreleme, oluşturma ve düzenleme alanıdır.
            - Sınav Oluştur: soru bankasındaki aktif sorularla sınav oluşturur; sınav bilgileri, öğrenciler, soru havuzu ve denge adımlarını takip eder.
            - Öğrenim Çıktıları: ders bazlı ÖÇ tanımlarını yönetir.
            - Program Çıktıları: program çıktısı tanımlarını yönetir.
            - PÇ - ÖÇ Eşleştirme: öğrenim çıktılarının program çıktılarına katkı seviyesini belirler.
            - PÇ Başarı Raporu: program çıktısı başarılarını analiz eder.
            - Öğrenciler: öğrenci kayıtlarını ve sınıf/bölüm bilgilerini yönetir.
            - AI Ayarları: provider, model ve API anahtarı seçimi ile bağlantı testi içindir.

            Cevap stili:
            - Türkçe cevap ver; kullanıcı İngilizce sorarsa İngilizce yanıtla.
            - Kısa, net ve profesyonel ol.
            - Gerektiğinde madde işaretleri kullan.
            - Uygulama içi işlem sorularında menü adlarını aynen kullan.
            - Markdown baslik isaretleri (#, ###), kalin yazim yildizlari (**) ve kod blogu kullanma; duz metin, kisa basliklar ve sade satirlar kullan.
            """;

        if (string.IsNullOrWhiteSpace(activeViewContext))
            return basePrompt;

        return basePrompt + "\n\n## Aktif Ekran Bağlamı\n" + activeViewContext;
    }

    private static string BuildSuggestionPrompt(string? activeViewContext, string? lastQuestion, string? lastAnswer)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Aktif ekranı tara ve kullanıcının bu panelde sorabileceği 3 net soru öner.");
        builder.AppendLine("Sorular uygulamadaki mevcut ekran, veri ve iş akışıyla ilgili olsun.");

        if (!string.IsNullOrWhiteSpace(activeViewContext))
        {
            builder.AppendLine();
            builder.AppendLine("Aktif ekran bağlamı:");
            builder.AppendLine(activeViewContext);
        }

        if (!string.IsNullOrWhiteSpace(lastQuestion) || !string.IsNullOrWhiteSpace(lastAnswer))
        {
            builder.AppendLine();
            builder.AppendLine("Son kullanıcı sorusu:");
            builder.AppendLine(lastQuestion ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Son asistan cevabı:");
            builder.AppendLine(lastAnswer ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Yeni 3 soru, bu cevabın doğal devamı gibi olsun.");
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> ParseSuggestedPrompts(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        return text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Select(line => System.Text.RegularExpressions.Regex.Replace(line, @"^\s*(?:[-*•]|\d+[.)])\s*", string.Empty))
            .Select(line => line.Trim().Trim('"', '\'', '`'))
            .Where(line => line.EndsWith('?') && line.Length >= 12)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Take(3)
            .ToList();
    }
}
