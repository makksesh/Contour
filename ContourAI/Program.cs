/// <summary>
/// Главная точка входа Avalonia 12.x приложения.
/// Регистрирует DI-контейнер с поддержкой session-слоя Фазы 3, Projects-слоя Фазы 4 и Documents-слоя Фазы 6.
/// Проект: DevAssistant / ContourAI.
/// </summary>
using System;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using ContourAI.Features.Auth;
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

        // API-сервисы
        services.AddSingleton<AuthService>();
        services.AddSingleton<DashboardService>();
        services.AddSingleton<ProjectsService>();
        services.AddSingleton<DocumentsService>();   // Фаза 6

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<CreateProjectDialogViewModel>();
        services.AddSingleton<ProjectsViewModel>();
        services.AddSingleton<DocumentsViewModel>();  // Фаза 6
        services.AddSingleton<AuthenticatedShellViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        // Window
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
