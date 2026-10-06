using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace EduAnalytics.UI.Services.AIAssistant;

public sealed class ClaudeProvider : IAIProvider
{
    public AIProviderKind Kind => AIProviderKind.Claude;

    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

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
            throw new AIException("Claude API anahtarı tanımlı değil. Ayarlardan ekleyin.");

        // Claude expects a flat messages array — no system role; system goes to top-level field.
        var messages = request.Messages
            .Where(m => m.Role != ChatRole.System)
            .Select(m => new
            {
                role = m.Role == ChatRole.User ? "user" : "assistant",
                content = m.Content
            })
            .ToArray();

        var payload = new
        {
            model = request.Model,
            max_tokens = request.MaxTokens,
            stream = true,
            system = request.SystemPrompt ?? string.Empty,
            messages
        };

        var json = JsonSerializer.Serialize(payload);
        using var http = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        http.Headers.Add("x-api-key", apiKey);
        http.Headers.Add("anthropic-version", AnthropicVersion);
        http.Headers.Add("accept", "text/event-stream");

        using var response = await _http.SendAsync(http, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new AIException($"Claude API hatası ({(int)response.StatusCode}): {body}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        // SSE format: lines starting with "data: " carry payloads; blank line separates events.
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
                if (!doc.RootElement.TryGetProperty("type", out var typeProp)) continue;
                var type = typeProp.GetString();

                if (type == "content_block_delta" &&
                    doc.RootElement.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("text", out var textProp))
                {
                    var text = textProp.GetString();
                    if (!string.IsNullOrEmpty(text)) onDelta(text);
                }
                else if (type == "message_stop")
                {
                    break;
                }
            }
            catch (JsonException)
            {
                // skip malformed event
            }
        }
    }
}
