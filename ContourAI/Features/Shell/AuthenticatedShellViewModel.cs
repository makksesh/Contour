/// <summary>
/// ViewModel авторизованного shell-экрана.
/// Использует AuthSessionStore вместо прямого хранения токена.
/// Логаут идёт через SessionAuthService, который очищает сессию и вызывает backend logout.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Features.Auth;
using ContourAI.Features.Dashboard;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Features.Shell;

public sealed class AuthenticatedShellViewModel : ViewModelBase
{
    private readonly ConnectionSettingsStore _connectionSettingsStore;
    private readonly AuthSessionStore _sessionStore;
    private readonly SessionAuthService _sessionAuthService;

    public AuthenticatedShellViewModel(
        ConnectionSettingsStore connectionSettingsStore,
        AuthSessionStore sessionStore,
        SessionAuthService sessionAuthService,
        DashboardViewModel dashboardViewModel)
    {
        _connectionSettingsStore = connectionSettingsStore;
        _sessionStore = sessionStore;
        _sessionAuthService = sessionAuthService;
        Dashboard = dashboardViewModel;

        // Команда logout запускает async-поток
        LogoutCommand = new RelayCommand(() => _ = LogoutAsync());

        _connectionSettingsStore.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ConnectionSettingsStore.ServerIp)
                or nameof(ConnectionSettingsStore.ServerIpDisplay))
            {
                RaisePropertyChanged(nameof(ServerIpDisplay));
            }
        };

        // Отслеживаем смену username из store
        _sessionStore.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AuthSessionStore.CurrentUsername))
                RaisePropertyChanged(nameof(Username));
        };
    }

    public event Action? LogoutRequested;

    public DashboardViewModel Dashboard { get; }

    public ICommand LogoutCommand { get; }

    /// <summary>Username берётся из AuthSessionStore, не хранится локально.</summary>
    public string Username => _sessionStore.CurrentUsername;

    public string ServerIpDisplay => _connectionSettingsStore.ServerIpDisplay;

    /// <summary>
    /// Применяет token в AuthSessionStore и загружает dashboard.
    /// </summary>
    public async Task ApplyAuthAsync(AuthTokenDto authToken, CancellationToken cancellationToken = default)
    {
        _sessionStore.Apply(authToken);
        await Dashboard.LoadAsync(authToken.AccessToken, cancellationToken);
    }

    /// <summary>
    /// Выполняет logout через SessionAuthService.
    /// Очищает сессию и поднимает LogoutRequested.
    /// </summary>
    private async Task LogoutAsync()
    {
        await _sessionAuthService.LogoutAsync();
        Dashboard.Clear();
        LogoutRequested?.Invoke();
    }
}
