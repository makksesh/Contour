/// <summary>
/// Главная ViewModel окна приложения.
/// Управляет переходами между Login, Register и авторизованным shell.
/// Подписывается на SessionExpired из SessionAuthService —
/// при  401/403 автоматически перенаправляет на login.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System.Threading.Tasks;
using ContourAI.Features.Auth;
using ContourAI.Features.Shell;
using ContourAI.Shared.Api;

namespace ContourAI;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly LoginViewModel _loginViewModel;
    private readonly RegisterViewModel _registerViewModel;
    private readonly AuthenticatedShellViewModel _authenticatedShellViewModel;
    private readonly SessionAuthService _sessionAuthService;
    private object? _currentViewModel;

    public MainWindowViewModel(
        LoginViewModel loginViewModel,
        RegisterViewModel registerViewModel,
        AuthenticatedShellViewModel authenticatedShellViewModel,
        SessionAuthService sessionAuthService)
    {
        _loginViewModel = loginViewModel;
        _registerViewModel = registerViewModel;
        _authenticatedShellViewModel = authenticatedShellViewModel;
        _sessionAuthService = sessionAuthService;

        _loginViewModel.RegisterRequested += ShowRegister;
        _registerViewModel.LoginRequested += ShowLogin;
        _loginViewModel.LoginSucceeded += authToken => _ = OnLoginSucceededAsync(authToken);
        _registerViewModel.RegisterSucceeded += authToken => _ = OnRegisterSucceededAsync(authToken);
        _authenticatedShellViewModel.LogoutRequested += ShowLogin;

        // При истечении сессии (401/403) автоматически возвращаемся на login
        _sessionAuthService.SessionExpired += ShowLogin;

        CurrentViewModel = _loginViewModel;
    }

    public object? CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    private void ShowLogin()
    {
        CurrentViewModel = _loginViewModel;
    }

    private void ShowRegister()
    {
        CurrentViewModel = _registerViewModel;
    }

    private async Task OnLoginSucceededAsync(AuthTokenDto authToken)
    {
        await _authenticatedShellViewModel.ApplyAuthAsync(authToken);
        CurrentViewModel = _authenticatedShellViewModel;
    }

    private async Task OnRegisterSucceededAsync(AuthTokenDto authToken)
    {
        await _authenticatedShellViewModel.ApplyAuthAsync(authToken);
        CurrentViewModel = _authenticatedShellViewModel;
    }
}
