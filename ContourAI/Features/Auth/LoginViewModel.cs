/// <summary>
/// ViewModel экрана входа.
/// Поддерживает редактирование IP сервера, логин пользователя и навигацию на экран регистрации.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Features.Auth;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly ConnectionSettingsStore _connectionSettingsStore;
    private string _identifier = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public LoginViewModel(AuthService authService, ConnectionSettingsStore connectionSettingsStore)
    {
        _authService = authService;
        _connectionSettingsStore = connectionSettingsStore;

        LoginCommand = new AsyncRelayCommand(LoginAsync, CanLogin);
        OpenRegisterCommand = new RelayCommand(OpenRegister);

        _connectionSettingsStore.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ConnectionSettingsStore.ServerIp) or nameof(ConnectionSettingsStore.ServerIpDisplay))
            {
                RaisePropertyChanged(nameof(ServerIp));
                RaisePropertyChanged(nameof(ServerIpDisplay));
            }
        };
    }

    public event Action? RegisterRequested;
    public event Action<AuthTokenDto>? LoginSucceeded;

    public ICommand LoginCommand { get; }

    public ICommand OpenRegisterCommand { get; }

    public string ServerIp
    {
        get => _connectionSettingsStore.ServerIp;
        set
        {
            _connectionSettingsStore.ServerIp = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ServerIpDisplay));
            RefreshCommands();
        }
    }

    public string ServerIpDisplay => _connectionSettingsStore.ServerIpDisplay;

    public string Identifier
    {
        get => _identifier;
        set
        {
            if (SetProperty(ref _identifier, value))
            {
                RefreshCommands();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                RefreshCommands();
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                RaisePropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommands();
            }
        }
    }

    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var result = await _authService.LoginAsync(Identifier.Trim(), Password);
            if (result is null)
            {
                ErrorMessage = "Сервер не вернул данные авторизации.";
                return;
            }

            LoginSucceeded?.Invoke(result);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка входа: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLogin()
    {
        return !IsBusy
               && !string.IsNullOrWhiteSpace(ServerIp)
               && !string.IsNullOrWhiteSpace(Identifier)
               && !string.IsNullOrWhiteSpace(Password);
    }

    private void OpenRegister()
    {
        RegisterRequested?.Invoke();
    }

    private void RefreshCommands()
    {
        if (LoginCommand is AsyncRelayCommand asyncRelayCommand)
        {
            asyncRelayCommand.NotifyCanExecuteChanged();
        }
    }
}
