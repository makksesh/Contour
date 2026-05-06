/// <summary>
/// Главная точка входа Avalonia 12.x приложения.
/// Регистрирует DI-контейнер с поддержкой:
///   Фаза 3 — session/auth lifecycle
///   Фаза 4 — Projects
///   Фаза 5 — Chat (Global + Project)
///   Фаза 6 — Documents
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using ContourAI.Features.Auth;
using ContourAI.Features.Chat;
using ContourAI.Features.Dashboard;
using ContourAI.Features.Documents;
using ContourAI.Features.Projects;
using ContourAI.Features.Shell;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI;

internal static class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        Services = BuildServices();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // Инфраструктура
        services.AddSingleton<ConnectionSettingsStore>();

        // Фаза 3: session-слой
        services.AddSingleton<AuthSessionStore>();
        services.AddSingleton<AuthorizedHttpClientFactory>();
        services.AddSingleton<SessionAuthService>();

        // Фаза 4: project context
        services.AddSingleton<ProjectContextStore>();

        // Фаза 5: chat state
        services.AddSingleton<ChatStore>();

        // API-сервисы
        services.AddSingleton<AuthService>();
        services.AddSingleton<DashboardService>();
        services.AddSingleton<ProjectsService>();
        services.AddSingleton<DocumentsService>();
        services.AddSingleton<ChatService>();       

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<CreateProjectDialogViewModel>();
        services.AddSingleton<ProjectsViewModel>();
        services.AddSingleton<DocumentsViewModel>();
        services.AddSingleton<ChatViewModel>();     
        services.AddSingleton<AuthenticatedShellViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<ProjectWorkspaceViewModel>();

        // Window
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
