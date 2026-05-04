/// <summary>
/// ViewModel авторизованного shell-экрана.
/// Управляет навигацией между разделами: Dashboard и Projects.
/// Использует AuthSessionStore вместо прямого хранения токена.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Features.Auth;
using ContourAI.Features.Dashboard;
using ContourAI.Features.Projects;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Features.Shell;

public sealed class AuthenticatedShellViewModel : ViewModelBase
{
    private readonly ConnectionSettingsStore _connectionSettingsStore;
    private readonly AuthSessionStore _sessionStore;
    private readonly SessionAuthService _sessionAuthService;
    private readonly ProjectContextStore _projectContextStore;
    private object? _currentContent;

    public AuthenticatedShellViewModel(
        ConnectionSettingsStore connectionSettingsStore,
        AuthSessionStore sessionStore,
        SessionAuthService sessionAuthService,
        ProjectContextStore projectContextStore,
        DashboardViewModel dashboardViewModel,
        ProjectsViewModel projectsViewModel)
    {
        _connectionSettingsStore = connectionSettingsStore;
        _sessionStore = sessionStore;
        _sessionAuthService = sessionAuthService;
        _projectContextStore = projectContextStore;

        Dashboard = dashboardViewModel;
        Projects = projectsViewModel;

        LogoutCommand = new RelayCommand(() => _ = LogoutAsync());
        ShowDashboardCommand = new RelayCommand(() => ShowSection(Dashboard));
        ShowProjectsCommand = new AsyncRelayCommand(ShowProjectsAsync);

        _connectionSettingsStore.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ConnectionSettingsStore.ServerIp)
                or nameof(ConnectionSettingsStore.ServerIpDisplay))
                RaisePropertyChanged(nameof(ServerIpDisplay));
        };

        _sessionStore.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AuthSessionStore.CurrentUsername))
                RaisePropertyChanged(nameof(Username));
        };

        // Старт — показываем dashboard
        _currentContent = Dashboard;
    }

    public event Action? LogoutRequested;

    public DashboardViewModel Dashboard { get; }
    public ProjectsViewModel Projects { get; }

    public ICommand LogoutCommand { get; }
    public ICommand ShowDashboardCommand { get; }
    public ICommand ShowProjectsCommand { get; }

    public string Username => _sessionStore.CurrentUsername;
    public string ServerIpDisplay => _connectionSettingsStore.ServerIpDisplay;

    /// <summary>Текущий отображаемый раздел в центральной области shell.</summary>
    public object? CurrentContent
    {
        get => _currentContent;
        private set => SetProperty(ref _currentContent, value);
    }

    /// <summary>True, если активен Dashboard.</summary>
    public bool IsDashboardActive => CurrentContent is DashboardViewModel;

    /// <summary>True, если активен Projects.</summary>
    public bool IsProjectsActive => CurrentContent is ProjectsViewModel;

    /// <summary>
    /// Применяет токен в AuthSessionStore и загружает dashboard.
    /// </summary>
    public async Task ApplyAuthAsync(AuthTokenDto authToken, CancellationToken cancellationToken = default)
    {
        _sessionStore.Apply(authToken);
        _projectContextStore.Clear();
        CurrentContent = Dashboard;
        RaiseActiveFlags();
        await Dashboard.LoadAsync(authToken.AccessToken, cancellationToken);
    }

    private void ShowSection(object section)
    {
        CurrentContent = section;
        RaiseActiveFlags();
    }

    private async Task ShowProjectsAsync()
    {
        CurrentContent = Projects;
        RaiseActiveFlags();
        await Projects.InitializeAsync();
    }

    private async Task LogoutAsync()
    {
        await _sessionAuthService.LogoutAsync();
        _projectContextStore.Clear();
        Dashboard.Clear();
        LogoutRequested?.Invoke();
    }

    private void RaiseActiveFlags()
    {
        RaisePropertyChanged(nameof(IsDashboardActive));
        RaisePropertyChanged(nameof(IsProjectsActive));
    }
}
