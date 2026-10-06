namespace EduAnalytics.UI.Services.AIAssistant;

/// <summary>Provider implementations stream tokens via <paramref name="onDelta"/>.</summary>
public interface IAIProvider
{
    AIProviderKind Kind { get; }

    Task StreamCompletionAsync(
        AIRequest request,
        string apiKey,
        StreamDeltaHandler onDelta,
        CancellationToken ct);
}
