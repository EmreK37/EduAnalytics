using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.UI.Services;
using EduAnalytics.UI.Services.AIAssistant;

namespace EduAnalytics.UI.ViewModels;

public partial class AIAssistantViewModel : ObservableObject
{
    private readonly IAIAssistantService _ai;
    private readonly ToastService _toasts;
    private readonly IAppLogService _log;
    private readonly Func<string?> _contextResolver;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _suggestionCts;
    private int _suggestionRefreshSerial;

    public AIAssistantViewModel(
        IAIAssistantService ai,
        ToastService toasts,
        IAppLogService log,
        Func<string?> contextResolver)
    {
        _ai = ai;
        _toasts = toasts;
        _log = log;
        _contextResolver = contextResolver;
        Messages = new ObservableCollection<ChatMessageVm>();
        SuggestedPrompts = new ObservableCollection<string>();
        RefreshConfigStatus();
        RefreshSuggestions(null);
    }

    public ObservableCollection<ChatMessageVm> Messages { get; }
    public ObservableCollection<string> SuggestedPrompts { get; }

    [ObservableProperty]
    private string _suggestionsTitle = "Bu ekranda sorulabilecekler";

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private bool _isPanelOpen;

    [ObservableProperty]
    private string _activeContextLabel = string.Empty;

    [ObservableProperty]
    private string _activeProviderLabel = string.Empty;

    [ObservableProperty]
    private bool _hasConfiguredKey;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _logFilePath = string.Empty;

    public void RefreshConfigStatus()
    {
        HasConfiguredKey = _ai.HasConfiguredKey;
        var modelName = AIModelCatalog.All.FirstOrDefault(m => m.Id == _ai.ActiveModel)?.DisplayName
                        ?? _ai.ActiveModel;
        ActiveProviderLabel = $"{_ai.ActiveProvider} · {modelName}";
        LogFilePath = _ai.LogFilePath;
    }

    [RelayCommand]
    public void TogglePanel()
    {
        RefreshConfigStatus();
        if (IsPanelOpen)
        {
            IsPanelOpen = false;
            _log.Info("AI.Panel", "Panel close requested.");
            return;
        }

        OpenPanel("toggle");
    }

    private void OpenPanel(string source)
    {
        IsPanelOpen = true;
        OnPropertyChanged(nameof(IsPanelOpen));
        _log.Info("AI.Panel", $"Panel open requested. Source={source}, ProviderLabel={ActiveProviderLabel}, HasConfiguredKey={HasConfiguredKey}");

        if (!IsPanelOpen) return;

        RefreshContextLabel();

        if (!HasConfiguredKey)
        {
            ErrorMessage = "AI yanıtı almak için önce Yapay Zeka Ayarları bölümünden API anahtarı girin.";
            _toasts.Warning("AI anahtarı yok", "Yapay Zeka Ayarları bölümünden provider, model ve API anahtarını kontrol edin.");
        }
    }

    [RelayCommand]
    private async Task StartPromptAsync(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return;

        InputText = prompt.Trim();
        RefreshConfigStatus();
        OpenPanel("prompt");
        _toasts.Info("AI sorusu gönderiliyor", "Sağ panelde yanıt akışını izleyebilirsiniz.");

        if (SendCommand.CanExecute(null))
            await SendAsync();
    }

    public void RefreshContextLabel()
    {
        var ctx = _contextResolver();
        ActiveContextLabel = string.IsNullOrWhiteSpace(ctx)
            ? "Genel"
            : ctx.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Aktif ekran";
        RefreshSuggestions(ctx);
    }

    [RelayCommand]
    private void ClearConversation()
    {
        if (IsStreaming) return;
        Messages.Clear();
        ErrorMessage = null;
        RefreshContextLabel();
    }

    [RelayCommand]
    private void CopyLastAnswer()
    {
        var last = Messages.LastOrDefault(m => m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.Content));
        if (last == null) return;
        System.Windows.Clipboard.SetText(last.Content);
        _toasts.Success("Kopyalandı", "Son AI yanıtı panoya kopyalandı.");
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = (InputText ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text)) return;
        if (IsStreaming) return;

        RefreshConfigStatus();
        if (!HasConfiguredKey)
        {
            ErrorMessage = "API anahtarı tanımlı değil. Yapay Zeka Ayarları bölümünden anahtarı girip bağlantıyı test edin.";
            _log.Warning("AI.Send", "Send blocked because active provider has no configured API key.");
            _toasts.Warning("AI çalışmadı", "Aktif provider için kayıtlı API anahtarı yok.");
            return;
        }

        ErrorMessage = null;
        InputText = string.Empty;
        SuggestionsTitle = "Cevaba göre öneriler hazırlanıyor";

        var userMsg = new ChatMessageVm { Role = ChatRole.User, Content = text };
        Messages.Add(userMsg);

        var assistantMsg = new ChatMessageVm { Role = ChatRole.Assistant, Content = string.Empty };
        Messages.Add(assistantMsg);

        IsStreaming = true;
        SendCommand.NotifyCanExecuteChanged();

        _cts = new CancellationTokenSource();
        try
        {
            var history = Messages
                .Where(m => !ReferenceEquals(m, assistantMsg))
                .Select(m => new ChatMessage { Role = m.Role, Content = m.Content })
                .ToList();

            var ctx = _contextResolver();
            await _ai.SendAsync(history, ctx, delta =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    assistantMsg.Content += delta;
                });
            }, _cts.Token);
            assistantMsg.Content = CleanAssistantOutput(assistantMsg.Content);

            if (string.IsNullOrEmpty(assistantMsg.Content))
                assistantMsg.Content = "(Boş yanıt)";
            await RefreshFollowUpSuggestionsAsync(text, assistantMsg.Content, ctx);
        }
        catch (OperationCanceledException)
        {
            assistantMsg.Content += "\n\n_(İptal edildi)_";
            assistantMsg.Content = CleanAssistantOutput(assistantMsg.Content);
            await RefreshFollowUpSuggestionsAsync(text, assistantMsg.Content, _contextResolver());
            _log.Warning("AI.Send", "Request cancelled by user.");
        }
        catch (AIException ex)
        {
            Messages.Remove(assistantMsg);
            ErrorMessage = ex.Message;
            _toasts.Error("AI yanıt veremedi", CompactError(ex.Message));
        }
        catch (Exception ex)
        {
            Messages.Remove(assistantMsg);
            ErrorMessage = $"Beklenmeyen hata: {ex.Message}";
            _log.Error("AI.Send", "Unexpected UI error while sending AI request.", ex);
            _toasts.Error("AI beklenmeyen hata verdi", CompactError(ex.Message));
        }
        finally
        {
            IsStreaming = false;
            _cts?.Dispose();
            _cts = null;
            SendCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanSend() => !IsStreaming && !string.IsNullOrWhiteSpace(InputText);

    partial void OnInputTextChanged(string value) => SendCommand.NotifyCanExecuteChanged();

    private static string CompactError(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "Ayrıntı için log dosyasına bakın.";
        return message.Length <= 220 ? message : message[..220] + "...";
    }

    private void RefreshSuggestions(string? context)
    {
        _ = RefreshSuggestionsAsync(context);
    }

    private async Task RefreshSuggestionsAsync(string? context)
    {
        var serial = ++_suggestionRefreshSerial;
        _suggestionCts?.Cancel();
        _suggestionCts?.Dispose();
        _suggestionCts = new CancellationTokenSource();
        var ct = _suggestionCts.Token;

        SuggestionsTitle = "Sayfa taranıyor";
        SuggestedPrompts.Clear();

        if (!HasConfiguredKey)
        {
            SuggestionsTitle = "Bu ekranda sorulabilecekler";
            SetSuggestedPrompts(BuildSuggestions(context));
            return;
        }

        try
        {
            var prompts = await _ai.GenerateSuggestedPromptsAsync(context, null, null, ct);
            if (serial != _suggestionRefreshSerial || ct.IsCancellationRequested)
                return;

            SuggestionsTitle = "Bu ekranda sorulabilecekler";
            SetSuggestedPrompts(prompts.Concat(BuildSuggestions(context)));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.Warning("AI.Suggestions", $"Suggestion generation failed: {ex.Message}");
            if (serial == _suggestionRefreshSerial)
            {
                SuggestionsTitle = "Bu ekranda sorulabilecekler";
                SetSuggestedPrompts(BuildSuggestions(context));
            }
        }
    }

    private async Task RefreshFollowUpSuggestionsAsync(string question, string answer, string? context)
    {
        var serial = ++_suggestionRefreshSerial;
        _suggestionCts?.Cancel();
        _suggestionCts?.Dispose();
        _suggestionCts = new CancellationTokenSource();
        var ct = _suggestionCts.Token;

        SuggestionsTitle = "Cevaba göre öneriler hazırlanıyor";
        SuggestedPrompts.Clear();

        if (!HasConfiguredKey)
        {
            SuggestionsTitle = "Bu cevaptan sonra sorulabilecekler";
            SetSuggestedPrompts(BuildFollowUpSuggestions(question, answer, context, serial));
            return;
        }

        try
        {
            var prompts = await _ai.GenerateSuggestedPromptsAsync(context, question, answer, ct);
            if (serial != _suggestionRefreshSerial || ct.IsCancellationRequested)
                return;

            SuggestionsTitle = "Bu cevaptan sonra sorulabilecekler";
            SetSuggestedPrompts(prompts.Concat(BuildFollowUpSuggestions(question, answer, context, serial)));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.Warning("AI.Suggestions", $"Follow-up suggestion generation failed: {ex.Message}");
            if (serial == _suggestionRefreshSerial)
            {
                SuggestionsTitle = "Bu cevaptan sonra sorulabilecekler";
                SetSuggestedPrompts(BuildFollowUpSuggestions(question, answer, context, serial));
            }
        }
    }

    private void SetSuggestedPrompts(IEnumerable<string> prompts)
    {
        SuggestedPrompts.Clear();
        foreach (var prompt in prompts
                     .Where(p => !string.IsNullOrWhiteSpace(p))
                     .Select(p => p.Trim())
                     .Distinct()
                     .Take(3))
        {
            SuggestedPrompts.Add(prompt);
        }
    }

    private static IReadOnlyList<string> BuildSuggestions(string? context)
    {
        var normalized = (context ?? string.Empty).ToLowerInvariant();

        if (ContainsAny(normalized, "dashboard"))
        {
            return new[]
            {
                "Dashboard'daki genel başarı metriklerini nasıl yorumlamalıyım?",
                "Hangi sınavların performans analizine öncelik vermeliyim?",
                "Düşük başarı veya risk sinyallerini bu ekranda nasıl fark ederim?"
            };
        }

        if (ContainsAny(normalized, "sınav analizi", "sinav analizi", "analiz"))
        {
            return new[]
            {
                "Bu sınavdaki en kritik kalite sinyallerini özetle.",
                "Hangi sorular revize edilmeli ve neden?",
                "Öğrenim çıktısı başarısına göre ders içi aksiyon öner."
            };
        }

        if (ContainsAny(normalized, "bankadan sınav", "bankadan sinav"))
        {
            return new[]
            {
                "Bu ekranda dengeli bir sınavı nasıl oluştururum?",
                "Seçtiğim soruların puan ve kapsam dengesini nasıl kontrol ederim?",
                "Soru havuzu ve öğrenci seçiminde nelere dikkat etmeliyim?"
            };
        }

        if (ContainsAny(normalized, "soru bankası", "soru bankasi"))
        {
            return new[]
            {
                "Soru bankasında kaliteli soru seçimi için hangi filtreleri kullanmalıyım?",
                "Bir sorunun öğrenim çıktısı ve konu bağlantısını nasıl kontrol ederim?",
                "Revize edilmesi gereken soru adaylarını nasıl belirlerim?"
            };
        }

        if (ContainsAny(normalized, "sınav yönetimi", "sinav yönetimi", "sinav yonetimi"))
        {
            return new[]
            {
                "Bu ekranda hangi sınavları önce incelemeliyim?",
                "Sınav analizine geçmeden önce hangi bilgileri kontrol etmeliyim?",
                "Eksik cevap girişi veya rapor sorunu varsa nasıl ilerlerim?"
            };
        }

        if (ContainsAny(normalized, "öğrenim çıktıları", "ogrenim ciktilari", "öç", "oc"))
        {
            return new[]
            {
                "Öğrenim çıktısı tanımlarını daha ölçülebilir hale nasıl getiririm?",
                "Bir ders için eksik veya çakışan ÖÇ'leri nasıl fark ederim?",
                "ÖÇ başarı raporlarını yorumlamak için nelere bakmalıyım?"
            };
        }

        if (ContainsAny(normalized, "program çıktıları", "program ciktilari", "pç başarı", "pc basari"))
        {
            return new[]
            {
                "Program çıktısı tanımlarını nasıl daha net hale getiririm?",
                "PÇ başarı raporunda düşük kalan alanları nasıl yorumlarım?",
                "Derslerin programa katkısını kontrol etmek için neye bakmalıyım?"
            };
        }

        if (ContainsAny(normalized, "pç - öç", "pc - oc", "eşleştirme", "eslestirme"))
        {
            return new[]
            {
                "ÖÇ ile PÇ katkı seviyelerini nasıl belirlemeliyim?",
                "Katkı matrisi tutarsız görünüyorsa nasıl düzeltirim?",
                "Bir dersin program çıktısına etkisini nasıl yorumlarım?"
            };
        }

        if (ContainsAny(normalized, "öğrenci", "ogrenci"))
        {
            return new[]
            {
                "Öğrenci listesini sınıf veya bölüm bazında nasıl düzenlerim?",
                "Sınav katılım kapsamını belirlerken nelere dikkat etmeliyim?",
                "Öğrenci performansını raporlara nasıl bağlarım?"
            };
        }

        if (ContainsAny(normalized, "akademik yapı", "akademik yapi"))
        {
            return new[]
            {
                "Program, ders ve sınıf yapısını doğru kurmak için sırayla ne yapmalıyım?",
                "Akademik yapıdaki eksik bağlantıları nasıl kontrol ederim?",
                "Dersleri çıktı yönetimiyle nasıl ilişkilendiririm?"
            };
        }

        if (ContainsAny(normalized, "cevap girişi", "cevap girisi"))
        {
            return new[]
            {
                "Cevap girişi yaparken hata riskini nasıl azaltırım?",
                "Eksik öğrenci veya soru puanı varsa nasıl kontrol ederim?",
                "Cevaplar kaydedildikten sonra hangi analize bakmalıyım?"
            };
        }

        if (ContainsAny(normalized, "soru oluşturma", "soru olusturma", "ortak köklü", "ortak koklu"))
        {
            return new[]
            {
                "Bu soru için uygun puan ve öğrenim çıktısı bağlantısını nasıl seçerim?",
                "Soru metnini daha ölçülebilir hale nasıl getiririm?",
                "Seçenekleri ve doğru cevabı kalite açısından nasıl kontrol ederim?"
            };
        }

        return new[]
        {
            "Bu ekranda ilk olarak neye bakmalıyım?",
            "Bu menüde sık yapılan hatalar nelerdir?",
            "Buradaki verileri nasıl yorumlamalıyım?"
        };
    }

    private static IReadOnlyList<string> BuildFollowUpSuggestions(string question, string answer, string? context, int version)
    {
        var source = $"{question} {answer} {context}".ToLowerInvariant();
        var prompts = new List<string>();

        if (ContainsAny(source, "cronbach", "alfa", "güvenilir", "guvenilir", "tutarlılık", "tutarlilik"))
        {
            prompts.Add("Güvenilirliği artırmak için hangi maddeleri önce incelemeliyim?");
            prompts.Add("Cronbach alfa sonucunu akademik kurul raporuna nasıl yazmalıyım?");
            prompts.Add("Sınav iç tutarlılığını yükseltmek için uygulanabilir aksiyon listesi çıkar.");
        }

        if (ContainsAny(source, "madde", "ayırt", "ayirt", "güçlük", "gucluk", "kolay", "zor", "çeldirici", "celdirici"))
        {
            prompts.Add("Madde analizine göre hangi sorular öncelikli revize edilmeli?");
            prompts.Add("Ayırt ediciliği düşük soruları nasıl iyileştirebilirim?");
            prompts.Add("Çok kolay veya çok zor maddeler için nasıl düzenleme önerirsin?");
        }

        if (ContainsAny(source, "öğrenim çıkt", "ogrenim cikt", "öç", "oc", "kazanım", "kazanim"))
        {
            prompts.Add("Zayıf öğrenim çıktıları için ders içi iyileştirme önerileri ver.");
            prompts.Add("Bu ÖÇ başarısını yükseltmek için hangi soru türleri eklenmeli?");
            prompts.Add("Öğrenim çıktısı bazlı kısa bir aksiyon planı hazırla.");
        }

        if (ContainsAny(source, "denge", "kapsam", "dağılım", "dagilim", "puan"))
        {
            prompts.Add("Sınav dengesini iyileştirmek için soru ve puan dağılımını nasıl değiştirmeliyim?");
            prompts.Add("Kapsam eksiklerini kapatmak için hangi öğrenim çıktıları güçlendirilmeli?");
            prompts.Add("100 puanlık dağılımı daha dengeli hale getirmek için öneri ver.");
        }

        if (ContainsAny(source, "rapor", "kurul", "değerlendirme", "degerlendirme", "özet", "ozet"))
        {
            prompts.Add("Bu sonucu akademik kurul için kısa ve profesyonel bir paragrafla yaz.");
            prompts.Add("Bu analizden yönetici özeti çıkar.");
            prompts.Add("Riskleri ve önerilen aksiyonları madde madde raporla.");
        }

        if (ContainsAny(source, "nasıl", "nasil", "adım", "adim", "kullan", "menü", "menu"))
        {
            prompts.Add("Bu işlemi uygulamada adım adım nasıl yaparım?");
            prompts.Add("Bu ekranda sık yapılan hatalar nelerdir?");
            prompts.Add("Bu işlemden sonra hangi ekrana geçmeliyim?");
        }

        if (prompts.Count < 3)
            prompts.AddRange(BuildSuggestions(context));

        prompts.AddRange(new[]
        {
            "Bunu daha kısa bir aksiyon listesine dönüştür.",
            "Bu sonucu rapor dilinde yeniden yazar mısın?",
            "Uygulamada bundan sonra hangi adımı atmalıyım?"
        });

        var distinctPrompts = prompts
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct()
            .ToList();

        if (distinctPrompts.Count == 0)
            return BuildSuggestions(context);

        var offset = version % distinctPrompts.Count;
        return distinctPrompts
            .Skip(offset)
            .Concat(distinctPrompts.Take(offset))
            .ToList();
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(value.Contains);

    private static string CleanAssistantOutput(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = Regex.Replace(normalized, @"^\s{0,3}#{1,6}\s*", string.Empty, RegexOptions.Multiline);
        normalized = Regex.Replace(normalized, @"^\s*[-*]\s+", "- ", RegexOptions.Multiline);
        normalized = Regex.Replace(normalized, @"\*\*(.*?)\*\*", "$1");
        normalized = Regex.Replace(normalized, @"__(.*?)__", "$1");
        normalized = Regex.Replace(normalized, @"`([^`]*)`", "$1");
        normalized = Regex.Replace(normalized, @"\[(.*?)\]\((.*?)\)", "$1");
        normalized = normalized.Replace("###", string.Empty)
            .Replace("##", string.Empty)
            .Replace("#", string.Empty)
            .Replace("**", string.Empty)
            .Replace("*", string.Empty)
            .Replace("_", string.Empty);
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");

        return normalized.Trim();
    }
}

public partial class ChatMessageVm : ObservableObject
{
    [ObservableProperty]
    private ChatRole _role;

    [ObservableProperty]
    private string _content = string.Empty;

    public bool IsUser => Role == ChatRole.User;
    public bool IsAssistant => Role == ChatRole.Assistant;

    partial void OnRoleChanged(ChatRole value)
    {
        OnPropertyChanged(nameof(IsUser));
        OnPropertyChanged(nameof(IsAssistant));
    }
}
