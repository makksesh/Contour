/// <summary>
/// ViewModel авторизованного shell-экрана.
/// Хранит текущего пользователя, состояние dashboard и команду выхода.
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
    private AuthTokenDto? _authToken;
    private string _username = "User";

    public AuthenticatedShellViewModel(ConnectionSettingsStore connectionSettingsStore, DashboardViewModel dashboardViewModel)
    {
        _connectionSettingsStore = connectionSettingsStore;
        Dashboard = dashboardViewModel;
        LogoutCommand = new RelayCommand(Logout);

        _connectionSettingsStore.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ConnectionSettingsStore.ServerIp) or nameof(ConnectionSettingsStore.ServerIpDisplay))
            {
                RaisePropertyChanged(nameof(ServerIpDisplay));
            }
        };
    }

    public event Action? LogoutRequested;

    public DashboardViewModel Dashboard { get; }

    public ICommand LogoutCommand { get; }

    public string Username
    {
        get => _username;
        private set => SetProperty(ref _username, value);
    }

    public string ServerIpDisplay => _connectionSettingsStore.ServerIpDisplay;

    public async Task ApplyAuthAsync(AuthTokenDto authToken, CancellationToken cancellationToken = default)
    {
        _authToken = authToken;
        Username = authToken.Username;
        await Dashboard.LoadAsync(authToken.AccessToken, cancellationToken);
    }

    private void Logout()
    {
        _authToken = null;
        Username = "User";
        Dashboard.Clear();
        LogoutRequested?.Invoke();
    }
}
