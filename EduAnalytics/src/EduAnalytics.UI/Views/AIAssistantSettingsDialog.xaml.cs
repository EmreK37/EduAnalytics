using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EduAnalytics.UI.Services;
using EduAnalytics.UI.Services.AIAssistant;
using Microsoft.Extensions.DependencyInjection;

namespace EduAnalytics.UI.Views;

public partial class AIAssistantSettingsDialog : Window
{
    private readonly IAISettingsService _settings;
    private readonly IAIAssistantService _ai;
    private readonly ToastService _toasts;
    private readonly IAppLogService _log;

    public AIAssistantSettingsDialog()
    {
        InitializeComponent();
        _settings = App.Services.GetRequiredService<IAISettingsService>();
        _ai = App.Services.GetRequiredService<IAIAssistantService>();
        _toasts = App.Services.GetRequiredService<ToastService>();
        _log = App.Services.GetRequiredService<IAppLogService>();

        ProviderCombo.ItemsSource = Enum.GetValues<AIProviderKind>();
        ProviderCombo.SelectedItem = _settings.Current.ActiveProvider;
        LoadModels(_settings.Current.ActiveProvider);
        UpdateKeyStatus(_settings.Current.ActiveProvider);
        RefreshSavedConnectionsList();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
    }

    private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProviderCombo.SelectedItem is not AIProviderKind provider) return;
        LoadModels(provider);
        UpdateKeyStatus(provider);
        ApiKeyBox.Clear();
        ConnectionStatusBorder.Visibility = Visibility.Collapsed;
    }

    private void LoadModels(AIProviderKind provider)
    {
        var models = AIModelCatalog.ForProvider(provider).ToList();
        ModelCombo.ItemsSource = models;

        var savedModel = GetModelIdForProvider(provider);
        ModelCombo.SelectedItem = models.FirstOrDefault(m => m.Id == savedModel)
                                  ?? models.FirstOrDefault();
    }

    private void UpdateKeyStatus(AIProviderKind provider)
    {
        KeyStatusText.Text = string.IsNullOrWhiteSpace(_settings.GetApiKey(provider))
            ? "Bu provider için kayıtlı API anahtarı yok."
            : "Bu provider için kayıtlı bir API anahtarı var. Değiştirmek için yeni anahtarı gir.";
    }

    private void ClearKey_Click(object sender, RoutedEventArgs e)
    {
        if (ProviderCombo.SelectedItem is not AIProviderKind provider) return;

        _settings.SetApiKey(provider, string.Empty);
        ApiKeyBox.Clear();
        UpdateKeyStatus(provider);
        RefreshSavedConnectionsList();
        ShowConnectionStatus(
            $"{provider} API anahtarı temizlendi.",
            "AlertInfoStyle",
            "AlertInfoTextStyle");
        _log.Info("AI.Settings", $"API key cleared. Provider={provider}");
        _toasts.Info("AI anahtarı temizlendi", $"{provider} için kayıtlı anahtar silindi.");
    }

    private void RefreshSavedConnections_Click(object sender, RoutedEventArgs e)
    {
        RefreshSavedConnectionsList();
    }

    private void EditSavedConnection_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not SavedAIConnectionRow row) return;

        ProviderCombo.SelectedItem = row.Provider;
        SelectModel(row.Provider, row.ModelId);
        ApiKeyBox.Clear();
        UpdateKeyStatus(row.Provider);
        ShowConnectionStatus(
            $"{row.ProviderLabel} kaydı düzenleme için forma yüklendi. API anahtarını değiştirmek için yeni anahtar gir.",
            "AlertInfoStyle",
            "AlertInfoTextStyle");
        ApiKeyBox.Focus();
    }

    private void DeleteSavedConnection_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not SavedAIConnectionRow row) return;

        var result = AppMessageBox.Show(
            this,
            $"{row.ProviderLabel} bağlantısını silmek istiyor musunuz?\n\nBu işlem kayıtlı API anahtarını ve model seçimini kaldırır.",
            "AI Bağlantısı Sil",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        var settings = _settings.Current;
        var providerKey = row.Provider.ToString();
        settings.EncryptedKeys.Remove(providerKey);
        settings.ProviderModels.Remove(providerKey);
        EnsureActiveProviderAfterDelete(settings, row.Provider);
        _settings.Save(settings);

        ProviderCombo.SelectedItem = settings.ActiveProvider;
        LoadModels(settings.ActiveProvider);
        ApiKeyBox.Clear();
        UpdateKeyStatus(settings.ActiveProvider);
        RefreshSavedConnectionsList();

        ShowConnectionStatus(
            $"{row.ProviderLabel} bağlantısı silindi.",
            "AlertInfoStyle",
            "AlertInfoTextStyle");
        _log.Info("AI.Settings", $"Saved connection deleted. Provider={row.Provider}");
        _toasts.Info("AI bağlantısı silindi", $"{row.ProviderLabel} kaydı kaldırıldı.");
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        if (ProviderCombo.SelectedItem is not AIProviderKind provider) return;

        var modelId = ResolveModelId();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            ShowConnectionStatus(
                "Test için bir model seç.",
                "AlertErrorStyle",
                "AlertErrorTextStyle");
            return;
        }

        TestButton.IsEnabled = false;
        TestButton.Content = "Test Ediliyor...";
        ShowConnectionStatus(
            $"{provider} / {modelId} bağlantısı test ediliyor. Bu işlem birkaç saniye sürebilir.",
            "AlertInfoStyle",
            "AlertInfoTextStyle");

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            await _ai.TestConfigurationAsync(provider, modelId, ApiKeyBox.Password, cts.Token);

            ShowConnectionStatus(
                $"Bağlantı başarılı. {provider} / {modelId} modeli yanıt verdi.",
                "AlertSuccessStyle",
                "AlertSuccessTextStyle");
            _toasts.Success("AI bağlantısı başarılı", $"{provider} / {modelId} modeli yanıt verdi.");
        }
        catch (OperationCanceledException)
        {
            var message = "Bağlantı testi zaman aşımına uğradı. İnternet bağlantısını, API anahtarını ve model id değerini kontrol edin.";
            ShowConnectionStatus(message, "AlertErrorStyle", "AlertErrorTextStyle");
            _toasts.Error("AI bağlantı testi başarısız", message);
        }
        catch (AIException ex)
        {
            ShowConnectionStatus(ex.Message, "AlertErrorStyle", "AlertErrorTextStyle");
            _toasts.Error("AI bağlantı testi başarısız", CompactError(ex.Message));
        }
        catch (Exception ex)
        {
            var message = $"Beklenmeyen test hatası: {ex.Message}";
            ShowConnectionStatus(message, "AlertErrorStyle", "AlertErrorTextStyle");
            _log.Error("AI.Settings", "Unexpected error while testing AI configuration.", ex);
            _toasts.Error("AI testinde beklenmeyen hata", CompactError(ex.Message));
        }
        finally
        {
            TestButton.IsEnabled = true;
            TestButton.Content = "Bağlantıyı Test Et";
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ProviderCombo.SelectedItem is not AIProviderKind provider)
        {
            ShowConnectionStatus(
                "Provider seçmediniz. Lütfen önce bir provider seçin.",
                "AlertErrorStyle",
                "AlertErrorTextStyle");
            ProviderCombo.Focus();
            return;
        }

        var modelId = ResolveModelId();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            ShowConnectionStatus(
                "Model seçmediniz. Lütfen bu provider için bir model seçin.",
                "AlertErrorStyle",
                "AlertErrorTextStyle");
            ModelCombo.Focus();
            return;
        }

        var hasSavedApiKey = !string.IsNullOrWhiteSpace(_settings.GetApiKey(provider));
        var hasNewApiKey = !string.IsNullOrWhiteSpace(ApiKeyBox.Password);
        if (!hasSavedApiKey && !hasNewApiKey)
        {
            ShowConnectionStatus(
                "API anahtarını girmediniz. Bu provider için kayıt oluşturmak istiyorsanız API anahtarını girin.",
                "AlertErrorStyle",
                "AlertErrorTextStyle");
            ApiKeyBox.Focus();
            return;
        }

        var settings = _settings.Current;
        settings.ActiveProvider = provider;
        settings.ActiveModel = modelId;
        settings.ProviderModels[provider.ToString()] = modelId;
        _settings.Save(settings);

        if (!string.IsNullOrWhiteSpace(ApiKeyBox.Password))
            _settings.SetApiKey(provider, ApiKeyBox.Password.Trim());

        RefreshSavedConnectionsList();
        _log.Info("AI.Settings", $"Settings saved. Provider={provider}, Model={modelId}, ApiKeyChanged={!string.IsNullOrWhiteSpace(ApiKeyBox.Password)}");
        _toasts.Success("AI ayarları kaydedildi", $"{provider} / {modelId} aktif edildi. Bağlantıyı test ederek doğrulayabilirsiniz.");

        DialogResult = true;
        Close();
    }

    private string ResolveModelId()
    {
        return ModelCombo.SelectedItem is AIModelDescriptor selected
            ? selected.Id
            : string.Empty;
    }

    private void SelectModel(AIProviderKind provider, string modelId)
    {
        LoadModels(provider);
        var selected = AIModelCatalog.ForProvider(provider).FirstOrDefault(m => m.Id == modelId);
        if (selected != null)
            ModelCombo.SelectedItem = selected;
    }

    private void RefreshSavedConnectionsList()
    {
        var rows = Enum.GetValues<AIProviderKind>()
            .Select(BuildSavedConnectionRow)
            .Where(row => row.HasApiKey || row.HasModelPreference)
            .ToList();

        SavedConnectionsList.ItemsSource = rows;
        SavedConnectionsEmptyText.Visibility = rows.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private SavedAIConnectionRow BuildSavedConnectionRow(AIProviderKind provider)
    {
        var providerKey = provider.ToString();
        var apiKey = _settings.GetApiKey(provider);
        var modelId = GetModelIdForProvider(provider);

        return new SavedAIConnectionRow
        {
            Provider = provider,
            ProviderLabel = provider.ToString(),
            ModelId = modelId,
            ModelDisplayName = GetModelDisplayName(provider, modelId),
            ApiKeyMasked = MaskApiKey(apiKey),
            HasApiKey = !string.IsNullOrWhiteSpace(apiKey),
            HasModelPreference = _settings.Current.ProviderModels.ContainsKey(providerKey),
            IsActive = _settings.Current.ActiveProvider == provider
        };
    }

    private string GetModelIdForProvider(AIProviderKind provider)
        => GetModelIdForProvider(_settings.Current, provider);

    private static string GetModelIdForProvider(AISettings settings, AIProviderKind provider)
    {
        var providerKey = provider.ToString();
        if (settings.ProviderModels.TryGetValue(providerKey, out var savedModel) &&
            !string.IsNullOrWhiteSpace(savedModel))
        {
            return savedModel;
        }

        if (settings.ActiveProvider == provider && !string.IsNullOrWhiteSpace(settings.ActiveModel))
            return settings.ActiveModel;

        return AIModelCatalog.ForProvider(provider).FirstOrDefault()?.Id ?? string.Empty;
    }

    private static string GetModelDisplayName(AIProviderKind provider, string modelId)
    {
        var model = AIModelCatalog.ForProvider(provider).FirstOrDefault(m => m.Id == modelId)
                    ?? AIModelCatalog.All.FirstOrDefault(m => m.Id == modelId);
        return model?.DisplayName ?? modelId;
    }

    private static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "API anahtarı yok";

        return "API anahtarı kayıtlı";
    }

    private static void EnsureActiveProviderAfterDelete(AISettings settings, AIProviderKind deletedProvider)
    {
        if (settings.ActiveProvider != deletedProvider)
            return;

        AIProviderKind? fallbackProvider = null;
        foreach (var provider in Enum.GetValues<AIProviderKind>())
        {
            var providerKey = provider.ToString();
            if (provider == deletedProvider)
                continue;

            if (settings.EncryptedKeys.ContainsKey(providerKey) ||
                settings.ProviderModels.ContainsKey(providerKey))
            {
                fallbackProvider = provider;
                break;
            }
        }

        var nextProvider = fallbackProvider ?? AIProviderKind.Claude;
        settings.ActiveProvider = nextProvider;
        settings.ActiveModel = GetModelIdForProvider(settings, nextProvider);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowConnectionStatus(string message, string borderStyleKey, string textStyleKey)
    {
        ConnectionStatusBorder.Style = (Style)FindResource(borderStyleKey);
        ConnectionStatusText.Style = (Style)FindResource(textStyleKey);
        ConnectionStatusText.Text = message;
        ConnectionStatusBorder.Visibility = Visibility.Visible;
    }

    private static string CompactError(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "Ayrıntı için log dosyasına bakın.";
        return message.Length <= 220 ? message : message[..220] + "...";
    }
}

public sealed class SavedAIConnectionRow
{
    public required AIProviderKind Provider { get; init; }
    public required string ProviderLabel { get; init; }
    public required string ModelId { get; init; }
    public required string ModelDisplayName { get; init; }
    public required string ApiKeyMasked { get; init; }
    public bool HasApiKey { get; init; }
    public bool HasModelPreference { get; init; }
    public bool IsActive { get; init; }
}
