using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Features.Auth;

public sealed class RegisterViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly ConnectionSettingsStore _connectionSettingsStore;
    private string _username = string.Empty;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public RegisterViewModel(AuthService authService, ConnectionSettingsStore connectionSettingsStore)
    {
        _authService = authService;
        _connectionSettingsStore = connectionSettingsStore;

        RegisterCommand = new AsyncRelayCommand(RegisterAsync, CanRegister);
        OpenLoginCommand = new RelayCommand(OpenLogin);

        _connectionSettingsStore.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ConnectionSettingsStore.ServerIp) or nameof(ConnectionSettingsStore.ServerIpDisplay))
            {
                RaisePropertyChanged(nameof(ServerIpDisplay));
            }
        };
    }

    public event Action? LoginRequested;
    public event Action<AuthTokenDto>? RegisterSucceeded;

    public ICommand RegisterCommand { get; }

    public ICommand OpenLoginCommand { get; }

    public string ServerIpDisplay => _connectionSettingsStore.ServerIpDisplay;

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                RefreshCommands();
            }
        }
    }

    public string Email
    {
        get => _email;
        set
        {
            if (SetProperty(ref _email, value))
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

    private async Task RegisterAsync()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var result = await _authService.RegisterAsync(Username.Trim(), Email.Trim(), Password);
            if (result is null)
            {
                ErrorMessage = "Сервер не вернул данные регистрации.";
                return;
            }

            RegisterSucceeded?.Invoke(result);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка регистрации: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRegister()
    {
        return !IsBusy
               && !string.IsNullOrWhiteSpace(_connectionSettingsStore.ServerIp)
               && !string.IsNullOrWhiteSpace(Username)
               && !string.IsNullOrWhiteSpace(Email)
               && !string.IsNullOrWhiteSpace(Password);
    }

    private void OpenLogin()
    {
        LoginRequested?.Invoke();
    }

    private void RefreshCommands()
    {
        if (RegisterCommand is AsyncRelayCommand asyncRelayCommand)
        {
            asyncRelayCommand.NotifyCanExecuteChanged();
        }
    }
}
