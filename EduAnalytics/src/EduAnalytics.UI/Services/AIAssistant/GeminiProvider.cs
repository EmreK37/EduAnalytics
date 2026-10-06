using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EduAnalytics.UI.Services.AIAssistant;

public sealed class GeminiProvider : IAIProvider
{
    public AIProviderKind Kind => AIProviderKind.Gemini;

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromMinutes(2)
    };

    public async Task StreamCompletionAsync(
        AIRequest request,
        string apiKey,
        StreamDeltaHandler onDelta,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AIException("Gemini API anahtarı tanımlı değil. Ayarlardan ekleyin.");

        var endpoint =
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(request.Model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        var contents = request.Messages
            .Where(m => m.Role != ChatRole.System)
            .Select(m => new
            {
                role = m.Role == ChatRole.Assistant ? "model" : "user",
                parts = new[] { new { text = m.Content } }
            })
            .ToArray();

        var generationConfig = BuildGenerationConfig(request);
        var payload = new
        {
            systemInstruction = string.IsNullOrWhiteSpace(request.SystemPrompt)
                ? null
                : new { parts = new[] { new { text = request.SystemPrompt } } },
            contents,
            generationConfig
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        using var http = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _http.SendAsync(http, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new AIException($"Gemini API hatası ({(int)response.StatusCode}): {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.GetArrayLength() == 0)
            throw new AIException("Gemini yanıtında aday metin bulunamadı.");

        var candidate = candidates[0];
        var finishReason = candidate.TryGetProperty("finishReason", out var finishReasonProp)
            ? finishReasonProp.GetString()
            : null;

        if (!candidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts))
            throw new AIException(BuildEmptyResponseMessage(finishReason, "Yanıtta content/parts alanı yok."));

        var delivered = false;
        foreach (var part in parts.EnumerateArray())
        {
            if (!part.TryGetProperty("text", out var textProp)) continue;
            var text = textProp.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                delivered = true;
                onDelta(text);
            }
        }

        if (!delivered)
            throw new AIException(BuildEmptyResponseMessage(finishReason, "Yanıt parçalarında görünür metin yok."));
    }

    private static Dictionary<string, object> BuildGenerationConfig(AIRequest request)
    {
        var config = new Dictionary<string, object>
        {
            ["maxOutputTokens"] = Math.Max(request.MaxTokens, 256)
        };

        var thinkingConfig = BuildThinkingConfig(request.Model);
        if (thinkingConfig.Count > 0)
            config["thinkingConfig"] = thinkingConfig;

        return config;
    }

    private static Dictionary<string, object> BuildThinkingConfig(string model)
    {
        var id = model.Trim().ToLowerInvariant();

        if (id.StartsWith("gemini-3"))
        {
            return new Dictionary<string, object>
            {
                ["thinkingLevel"] = "low"
            };
        }

        if (id.StartsWith("gemini-2.5-flash"))
        {
            return new Dictionary<string, object>
            {
                ["thinkingBudget"] = 0
            };
        }

        return new Dictionary<string, object>();
    }

    private static string BuildEmptyResponseMessage(string? finishReason, string detail)
    {
        var reason = string.IsNullOrWhiteSpace(finishReason)
            ? "belirtilmedi"
            : finishReason;

        return $"Gemini bağlantısı kuruldu ancak model metin döndürmedi. Detay: {detail} FinishReason={reason}. Farklı bir model seçmeyi veya tekrar denemeyi deneyin.";
    }
}
