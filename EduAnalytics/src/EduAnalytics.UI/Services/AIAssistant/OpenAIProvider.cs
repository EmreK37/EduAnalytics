using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EduAnalytics.UI.Services.AIAssistant;

public sealed class OpenAIProvider : IAIProvider
{
    public AIProviderKind Kind => AIProviderKind.OpenAI;

    private const string Endpoint = "https://api.openai.com/v1/chat/completions";

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
            throw new AIException("OpenAI API anahtarı tanımlı değil. Ayarlardan ekleyin.");

        var msgList = new List<object>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            msgList.Add(new { role = "system", content = request.SystemPrompt });

        foreach (var m in request.Messages.Where(m => m.Role != ChatRole.System))
        {
            msgList.Add(new
            {
                role = m.Role == ChatRole.User ? "user" : "assistant",
                content = m.Content
            });
        }

        var payload = new
        {
            model = request.Model,
            stream = true,
            max_completion_tokens = request.MaxTokens,
            messages = msgList
        };

        var json = JsonSerializer.Serialize(payload);
        using var http = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        http.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        http.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _http.SendAsync(http, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new AIException($"OpenAI API hatası ({(int)response.StatusCode}): {body}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data:")) continue;

            var payloadJson = line["data:".Length..].Trim();
            if (payloadJson is "" or "[DONE]") continue;

            try
            {
                using var doc = JsonDocument.Parse(payloadJson);
                if (!doc.RootElement.TryGetProperty("choices", out var choices)) continue;
                if (choices.GetArrayLength() == 0) continue;

                var choice = choices[0];
                if (!choice.TryGetProperty("delta", out var delta)) continue;
                if (!delta.TryGetProperty("content", out var contentProp)) continue;

                var text = contentProp.GetString();
                if (!string.IsNullOrEmpty(text)) onDelta(text);
            }
            catch (JsonException)
            {
                // skip malformed event
            }
        }
    }
}
