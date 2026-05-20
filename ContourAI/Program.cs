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
using ContourAI.Features.Documents;
using ContourAI.Features.Projects;
using ContourAI.Features.Shell;
using ContourAI.Features.Workspace;
using ContourAI.Shared.Api;
using ContourAI.Shared.Client;
using ContourAI.Shared.State;
using ContourAI.Widgets.SystemMetrics;

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

        // Фаза 7: workspace state
        services.AddSingleton<WorkspaceStore>();

        // API-сервисы
        services.AddSingleton<AuthService>();
        services.AddSingleton<ProjectsService>();
        services.AddSingleton<DocumentsService>();
        services.AddSingleton<ChatService>();
        services.AddSingleton<IndexingService>();
        services.AddSingleton<SystemMetricsService>();
        services.AddSingleton<WorkspaceService>();

        // Workspace client services
        services.AddSingleton<ConflictResolutionService>();
        services.AddSingleton<ChangeSetApplyService>();
        services.AddSingleton<LocalWorkspaceSyncService>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddSingleton<SystemMetricsViewModel>();
        services.AddSingleton<CreateProjectDialogViewModel>();
        
        services.AddSingleton<ProjectsViewModel>(sp => new ProjectsViewModel(
            sp.GetRequiredService<ProjectsService>(),
            sp.GetRequiredService<ChatService>(),
            sp.GetRequiredService<ProjectContextStore>()));
        
        services.AddSingleton<DocumentsViewModel>();
        services.AddSingleton<ChatViewModel>();     
        services.AddSingleton<AuthenticatedShellViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<ProjectDocumentsViewModel>();
        
        services.AddSingleton<WorkspaceSyncViewModel>();
        services.AddSingleton<AgentTasksViewModel>();
        services.AddSingleton<ChangeSetReviewViewModel>();

        services.AddSingleton<ProjectWorkspaceViewModel>(sp => new ProjectWorkspaceViewModel(
            sp.GetRequiredService<ProjectsService>(),
            sp.GetRequiredService<ChatService>(),
            sp.GetRequiredService<ProjectContextStore>(),
            sp.GetRequiredService<ProjectDocumentsViewModel>(),
            sp.GetRequiredService<WorkspaceSyncViewModel>(),
            sp.GetRequiredService<AgentTasksViewModel>(),
            sp.GetRequiredService<ChangeSetReviewViewModel>()));
        

        // Window
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
