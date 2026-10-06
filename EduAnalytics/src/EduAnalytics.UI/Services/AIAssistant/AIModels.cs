namespace EduAnalytics.UI.Services.AIAssistant;

public enum AIProviderKind
{
    Claude,
    OpenAI,
    Gemini
}

public enum ChatRole
{
    User,
    Assistant,
    System
}

public sealed class ChatMessage
{
    public ChatRole Role { get; init; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

public sealed class AIModelDescriptor
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required AIProviderKind Provider { get; init; }
    public string Description { get; init; } = string.Empty;

    public override string ToString() => DisplayName;
}

public static class AIModelCatalog
{
    public static readonly IReadOnlyList<AIModelDescriptor> All = new List<AIModelDescriptor>
    {
        // Claude (Anthropic)
        new() { Id = "claude-opus-4-7",            DisplayName = "Claude Opus 4.7",       Provider = AIProviderKind.Claude, Description = "En güçlü Claude modeli; karmaşık muhakeme ve ajan işleri" },
        new() { Id = "claude-sonnet-4-6",          DisplayName = "Claude Sonnet 4.6",     Provider = AIProviderKind.Claude, Description = "Hız, kalite ve muhakeme dengesi" },
        new() { Id = "claude-haiku-4-5-20251001",  DisplayName = "Claude Haiku 4.5",      Provider = AIProviderKind.Claude, Description = "En hızlı Claude modeli; düşük gecikme" },
        new() { Id = "claude-opus-4-1-20250805",   DisplayName = "Claude Opus 4.1",       Provider = AIProviderKind.Claude, Description = "Önceki güçlü Opus sürümü" },
        new() { Id = "claude-opus-4-20250514",     DisplayName = "Claude Opus 4",         Provider = AIProviderKind.Claude, Description = "Güçlü muhakeme" },
        new() { Id = "claude-sonnet-4-20250514",   DisplayName = "Claude Sonnet 4",       Provider = AIProviderKind.Claude, Description = "Dengeli, yüksek performanslı model" },
        new() { Id = "claude-3-7-sonnet-20250219", DisplayName = "Claude Sonnet 3.7",     Provider = AIProviderKind.Claude, Description = "Güçlü ve hızlı eski Sonnet" },
        new() { Id = "claude-3-5-sonnet-20241022", DisplayName = "Claude Sonnet 3.5 v2",  Provider = AIProviderKind.Claude, Description = "Önceki akıllı Sonnet sürümü" },
        new() { Id = "claude-3-5-sonnet-20240620", DisplayName = "Claude Sonnet 3.5",     Provider = AIProviderKind.Claude, Description = "Eski Sonnet 3.5 snapshot" },
        new() { Id = "claude-3-5-haiku-20241022",  DisplayName = "Claude Haiku 3.5",      Provider = AIProviderKind.Claude, Description = "Hızlı ve uygun maliyetli" },
        new() { Id = "claude-3-haiku-20240307",    DisplayName = "Claude Haiku 3",        Provider = AIProviderKind.Claude, Description = "Eski, hızlı ve kompakt model" },

        // OpenAI
        new() { Id = "gpt-5.2",                    DisplayName = "GPT-5.2",              Provider = AIProviderKind.OpenAI, Description = "En yeni genel amaçlı model; zor görevler ve kodlama" },
        new() { Id = "gpt-5.2-pro",                DisplayName = "GPT-5.2 Pro",          Provider = AIProviderKind.OpenAI, Description = "Daha fazla düşünme isteyen zor problemler" },
        new() { Id = "gpt-5.2-chat-latest",        DisplayName = "GPT-5.2 Chat",         Provider = AIProviderKind.OpenAI, Description = "ChatGPT'deki güncel GPT-5.2 sohbet modeli" },
        new() { Id = "gpt-5.2-codex",              DisplayName = "GPT-5.2 Codex",        Provider = AIProviderKind.OpenAI, Description = "Kodlama ve ajan geliştirme odaklı" },
        new() { Id = "gpt-5.1",                    DisplayName = "GPT-5.1",              Provider = AIProviderKind.OpenAI, Description = "Önceki flagship muhakeme modeli" },
        new() { Id = "gpt-5",                      DisplayName = "GPT-5",                Provider = AIProviderKind.OpenAI, Description = "Güçlü muhakeme ve kodlama modeli" },
        new() { Id = "gpt-5-mini",                 DisplayName = "GPT-5 mini",           Provider = AIProviderKind.OpenAI, Description = "Dengeli, hızlı ve ekonomik GPT-5 modeli" },
        new() { Id = "gpt-5-nano",                 DisplayName = "GPT-5 nano",           Provider = AIProviderKind.OpenAI, Description = "Basit görevler için en hızlı ve ekonomik seçenek" },
        new() { Id = "gpt-4.1",                    DisplayName = "GPT-4.1",              Provider = AIProviderKind.OpenAI, Description = "Güçlü metin, araç kullanımı ve talimat takibi" },
        new() { Id = "gpt-4.1-mini",               DisplayName = "GPT-4.1 mini",         Provider = AIProviderKind.OpenAI, Description = "Dengeli ve ekonomik GPT-4.1" },
        new() { Id = "gpt-4.1-nano",               DisplayName = "GPT-4.1 nano",         Provider = AIProviderKind.OpenAI, Description = "Düşük maliyetli ve hızlı GPT-4.1" },
        new() { Id = "o4-mini",                    DisplayName = "o4-mini",              Provider = AIProviderKind.OpenAI, Description = "Hızlı ve maliyet etkin muhakeme modeli" },
        new() { Id = "o3",                         DisplayName = "o3",                   Provider = AIProviderKind.OpenAI, Description = "Güçlü muhakeme modeli" },
        new() { Id = "o3-mini",                    DisplayName = "o3-mini",              Provider = AIProviderKind.OpenAI, Description = "Küçük muhakeme modeli" },
        new() { Id = "gpt-4o",                     DisplayName = "GPT-4o",               Provider = AIProviderKind.OpenAI, Description = "Yaygın çok modlu model" },
        new() { Id = "gpt-4o-mini",                DisplayName = "GPT-4o mini",          Provider = AIProviderKind.OpenAI, Description = "Hızlı ve ekonomik çok modlu model" },
        new() { Id = "gpt-4-turbo",                DisplayName = "GPT-4 Turbo",          Provider = AIProviderKind.OpenAI, Description = "Eski ama güvenilir GPT-4 modeli" },
        new() { Id = "gpt-oss-120b",               DisplayName = "GPT-OSS 120B",         Provider = AIProviderKind.OpenAI, Description = "Open-weight büyük model" },
        new() { Id = "gpt-oss-20b",                DisplayName = "GPT-OSS 20B",          Provider = AIProviderKind.OpenAI, Description = "Open-weight hızlı/ekonomik model" },

        // Google Gemini
        new() { Id = "gemini-3-pro-preview",               DisplayName = "Gemini 3 Pro Preview",          Provider = AIProviderKind.Gemini, Description = "Gemini 3 ailesinin en güçlü muhakeme ve ajan modeli" },
        new() { Id = "gemini-3-flash-preview",             DisplayName = "Gemini 3 Flash Preview",        Provider = AIProviderKind.Gemini, Description = "Gemini 3 ailesinde hızlı, dengeli ve ölçeklenebilir model" },
        new() { Id = "gemini-3-pro-image-preview",         DisplayName = "Gemini 3 Pro Image Preview",    Provider = AIProviderKind.Gemini, Description = "Görsel üretim odaklı Gemini 3 modeli; metin sohbet için önerilmez" },
        new() { Id = "gemini-2.5-pro",                     DisplayName = "Gemini 2.5 Pro",                Provider = AIProviderKind.Gemini, Description = "Google'ın gelişmiş düşünme modeli; karmaşık analiz ve kodlama" },
        new() { Id = "gemini-2.5-flash",                   DisplayName = "Gemini 2.5 Flash",              Provider = AIProviderKind.Gemini, Description = "Fiyat/performans dengeli, hızlı ve akıllı model" },
        new() { Id = "gemini-2.5-flash-lite",              DisplayName = "Gemini 2.5 Flash-Lite",         Provider = AIProviderKind.Gemini, Description = "En hızlı ve maliyet etkin Gemini metin modeli" },
        new() { Id = "gemini-2.5-flash-preview-09-2025",   DisplayName = "Gemini 2.5 Flash Preview",      Provider = AIProviderKind.Gemini, Description = "Preview Flash modeli; üretimde dikkatli kullanın" },
        new() { Id = "gemini-2.5-flash-lite-preview-09-2025", DisplayName = "Gemini 2.5 Flash-Lite Preview", Provider = AIProviderKind.Gemini, Description = "Preview Flash-Lite modeli; düşük gecikme" },
        new() { Id = "gemini-2.0-flash",                   DisplayName = "Gemini 2.0 Flash",              Provider = AIProviderKind.Gemini, Description = "İkinci nesil hızlı workhorse model" },
        new() { Id = "gemini-2.0-flash-001",               DisplayName = "Gemini 2.0 Flash 001",          Provider = AIProviderKind.Gemini, Description = "Gemini 2.0 Flash stable snapshot" },
        new() { Id = "gemini-2.0-flash-lite",              DisplayName = "Gemini 2.0 Flash-Lite",         Provider = AIProviderKind.Gemini, Description = "Gemini 2.0 ailesinde hızlı ve ekonomik model" },
        new() { Id = "gemini-2.0-flash-lite-001",          DisplayName = "Gemini 2.0 Flash-Lite 001",     Provider = AIProviderKind.Gemini, Description = "Gemini 2.0 Flash-Lite stable snapshot" },
    };

    public static IEnumerable<AIModelDescriptor> ForProvider(AIProviderKind provider)
        => All.Where(m => m.Provider == provider);
}

public sealed class AIRequest
{
    public required string Model { get; init; }
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
    public string? SystemPrompt { get; init; }
    public int MaxTokens { get; init; } = 1024;
}

public delegate void StreamDeltaHandler(string deltaText);

public sealed class AIException : Exception
{
    public AIException(string message) : base(message) { }
    public AIException(string message, Exception inner) : base(message, inner) { }
}
