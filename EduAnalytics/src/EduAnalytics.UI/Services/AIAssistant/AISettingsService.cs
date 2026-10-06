using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EduAnalytics.UI.Services.AIAssistant;

public interface IAISettingsService
{
    AISettings Current { get; }
    void Save(AISettings settings);
    string? GetApiKey(AIProviderKind provider);
    void SetApiKey(AIProviderKind provider, string apiKey);
}

public sealed class AISettings
{
    public AIProviderKind ActiveProvider { get; set; } = AIProviderKind.Claude;
    public string ActiveModel { get; set; } = "claude-sonnet-4-6";
    /// <summary>Last selected model id per provider.</summary>
    public Dictionary<string, string> ProviderModels { get; set; } = new();
    /// <summary>Encrypted (DPAPI) base64-encoded API keys per provider.</summary>
    public Dictionary<string, string> EncryptedKeys { get; set; } = new();
}

public sealed class AISettingsService : IAISettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EduAnalytics", "ai-settings.json");

    // Entropy makes the encrypted blob useless if copied to another machine/user.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("EduAnalytics.AI.v1");

    public AISettings Current { get; private set; }

    public AISettingsService()
    {
        Current = Load();
    }

    public void Save(AISettings settings)
    {
        Current = settings;
        Persist();
    }

    public string? GetApiKey(AIProviderKind provider)
    {
        if (!Current.EncryptedKeys.TryGetValue(provider.ToString(), out var encrypted) ||
            string.IsNullOrEmpty(encrypted))
        {
            return null;
        }

        try
        {
            var cipher = Convert.FromBase64String(encrypted);
            var plain = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            // Key blob corrupted or migrated machine — treat as missing.
            return null;
        }
    }

    public void SetApiKey(AIProviderKind provider, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Current.EncryptedKeys.Remove(provider.ToString());
        }
        else
        {
            var plain = Encoding.UTF8.GetBytes(apiKey);
            var cipher = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            Current.EncryptedKeys[provider.ToString()] = Convert.ToBase64String(cipher);
        }
        Persist();
    }

    private static AISettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AISettings();
            var json = File.ReadAllText(SettingsPath);
            return Normalize(JsonSerializer.Deserialize<AISettings>(json) ?? new AISettings());
        }
        catch
        {
            return new AISettings();
        }
    }

    private static AISettings Normalize(AISettings settings)
    {
        settings.EncryptedKeys ??= new();
        settings.ProviderModels ??= new();
        settings.ActiveModel = NormalizeModelId(settings.ActiveModel);

        foreach (var key in settings.ProviderModels.Keys.ToList())
            settings.ProviderModels[key] = NormalizeModelId(settings.ProviderModels[key]);

        var activeProviderKey = settings.ActiveProvider.ToString();
        if (!settings.ProviderModels.ContainsKey(activeProviderKey) &&
            !string.IsNullOrWhiteSpace(settings.ActiveModel) &&
            settings.EncryptedKeys.ContainsKey(activeProviderKey))
        {
            settings.ProviderModels[activeProviderKey] = settings.ActiveModel;
        }

        return settings;
    }

    private static string NormalizeModelId(string? modelId)
    {
        return modelId switch
        {
            "claude-haiku-4-5" => "claude-haiku-4-5-20251001",
            null or "" => "claude-sonnet-4-6",
            _ => modelId
        };
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AISettings] Persist failed: {ex.Message}");
        }
    }
}
