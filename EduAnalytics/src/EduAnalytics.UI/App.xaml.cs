using System.Windows;
using System.Windows.Threading;
using System.Net.Http;
using EduAnalytics.Business.Services.Implementations;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.DataAccess.Context;
using EduAnalytics.DataAccess.Seed;
using EduAnalytics.Ocr.Reading;
using EduAnalytics.UI.Services;
using EduAnalytics.UI.Services.AIAssistant;
using EduAnalytics.UI.ViewModels;
using EduAnalytics.UI.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EduAnalytics.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            // Splash ve giriş ekranı sistemin o anki Light/Dark modunu takip eder.
            var themeService = Services.GetRequiredService<IThemeService>();
            themeService.ApplySystemForStartup();

            var splash = new SplashWindow();
            splash.Show();

            // Let WPF render the splash before blocking work starts
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

            Exception? startupError = null;
            await Task.WhenAll(
                Task.Run(() =>
                {
                    try
                    {
                        SeedDatabaseWithRetry();
                    }
                    catch (Exception ex)
                    {
                        startupError = ex;
                    }
                }),
                Task.Delay(600)
            );

            if (startupError != null)
            {
                splash.Close();
                AppMessageBox.Show(
                    $"Veritabanı başlatılamadı:\n\n{startupError.GetType().Name}: {startupError.Message}\n\n{startupError.InnerException?.Message}\n\nLocalDB hala açılmıyorsa SQL Server LocalDB instance'ını yeniden başlatmayı deneyin.",
                    "Başlangıç Hatası",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            await splash.FadeOutAndCloseAsync();

            var loginWindow = Services.GetRequiredService<LoginWindow>();
            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            // Ana uygulama açılırken kullanıcının uygulama içi tema tercihi varsa geri yüklenir.
            themeService.LoadAndApply();

            var mainWindow = Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            AppMessageBox.Show(
                $"Uygulama başlatılırken beklenmeyen bir hata oluştu:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.InnerException?.Message}",
                "Başlangıç Hatası",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
    }

    private static void SeedDatabaseWithRetry()
    {
        const int maxAttempts = 4;
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var ctx = Services.GetRequiredService<EduAnalyticsDbContext>();
                DbInitializer.Seed(ctx);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransientLocalDbStartupError(ex))
            {
                lastError = ex;
                Thread.Sleep(TimeSpan.FromMilliseconds(650 * attempt));
            }
        }

        throw lastError ?? new InvalidOperationException("Veritabanı başlatılamadı.");
    }

    private static bool IsTransientLocalDbStartupError(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            var message = current.Message.ToLowerInvariant();
            if (message.Contains("localdb") ||
                message.Contains("error: 50") ||
                message.Contains("0x89c5010a") ||
                message.Contains("failed to start") ||
                message.Contains("network-related") ||
                message.Contains("instance-specific"))
            {
                return true;
            }
        }

        return false;
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // DbContext - Transient: Her servis çağrısı kendi context'ini alır.
        // WPF masaüstü uygulamasında "Scope" kavramı yoktur, Transient en güvenli.
        services.AddDbContext<EduAnalyticsDbContext>(options =>
            options.UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;" +
                "Database=EduAnalyticsDb;" +
                "Trusted_Connection=true;" +
                "TrustServerCertificate=true;"),
            ServiceLifetime.Transient);

        // Business Services - Transient
        // ── FAZ 1 servisleri ──
        services.AddTransient<IExamAnalysisService, ExamAnalysisService>();
        services.AddTransient<IDistractorAnalysisService, DistractorAnalysisService>();
        services.AddTransient<ITopicPerformanceService, TopicPerformanceService>();
        services.AddTransient<IStudentPerformanceService, StudentPerformanceService>();
        services.AddTransient<IExamCrudService, ExamCrudService>();
        services.AddTransient<IAnswerEntryService, AnswerEntryService>();
        services.AddTransient<ILearningOutcomePerformanceService, LearningOutcomePerformanceService>();

        // ── FAZ 2 servisleri (ASOS yenileme) ──
        services.AddTransient<IQuestionBankService, QuestionBankService>();
        services.AddTransient<ILearningOutcomeService, LearningOutcomeService>();
        services.AddTransient<IProgramOutcomeService, ProgramOutcomeService>();
        services.AddTransient<IExamBookletService, ExamBookletService>();
        services.AddTransient<IExamBalanceCheckService, ExamBalanceCheckService>();
        services.AddTransient<IExamCancellationService, ExamCancellationService>();
        services.AddTransient<IStudentService, StudentService>();
        services.AddTransient<IAcademicStructureService, AcademicStructureService>();
        services.AddTransient<IExamStatisticsService, ExamStatisticsService>();
        services.AddTransient<IItemAnalysisService, ItemAnalysisService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ExamCalendarViewModel>();
        services.AddTransient<ExamAnalysisViewModel>();
        services.AddTransient<ExamCreateViewModel>();
        services.AddTransient<AnswerEntryViewModel>();

        // FAZ 3 — Yeni ASOS ekranları
        services.AddTransient<QuestionBankViewModel>();
        services.AddTransient<LearningOutcomeManagementViewModel>();
        services.AddTransient<ProgramOutcomeManagementViewModel>();
        services.AddTransient<ProgramOutcomeReportViewModel>();
        services.AddTransient<ProgramOutcomeMappingViewModel>();
        services.AddTransient<StudentManagementViewModel>();
        services.AddTransient<AcademicStructureViewModel>();

        // FAZ 4 — İleri özellikler
        services.AddTransient<QuestionGroupEditorViewModel>();
        services.AddTransient<ExamFromBankViewModel>();
        services.AddTransient<ExamManagementViewModel>();
        services.AddTransient<QuestionEditDialogViewModel>();
        services.AddTransient<SingleQuestionCreateViewModel>();

        // FAZ 5 — Rubric (klasik soru kriter-bazlı puanlama)
        services.AddTransient<IRubricService, RubricService>();
        services.AddTransient<RubricGradeDialogViewModel>();

        // FAZ 6 — Optik okuma (PaddleOCR + OMR)
        // Singleton: PaddleOCR modelleri ilk kullanımda yüklenir ve süreç boyunca bellekte kalır.
        services.AddSingleton<IOpticalFormReader, OpticalFormReader>();
        services.AddTransient<OpticalReadingViewModel>();

        // UI altyapı
        services.AddSingleton<IAppLogService, AppLogService>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IUserProfileService, UserProfileService>();
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(60) });
        services.AddSingleton<IAISettingsService, AISettingsService>();
        services.AddSingleton<IAIProvider, ClaudeProvider>();
        services.AddSingleton<IAIProvider, OpenAIProvider>();
        services.AddSingleton<IAIProvider, GeminiProvider>();
        services.AddSingleton<IAIAssistantService, AIAssistantService>();

        // Windows
        services.AddTransient<MainWindow>();
        services.AddTransient<LoginWindow>();
        services.AddTransient<UserSettingsDialog>();
    }
}
