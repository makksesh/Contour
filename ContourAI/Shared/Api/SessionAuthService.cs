using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ContourAI.Shared.State;

namespace ContourAI.Shared.Api;

public sealed class SessionAuthService
{
    private readonly AuthService      _authService;
    private readonly AuthSessionStore _sessionStore;

    /// <summary>
    /// Поднимается только когда refresh токена провалился или пользователь явно разлогинился.
    /// Подписчик (MainWindowViewModel) делает redirect на экран Login.
    /// </summary>
    public event Action? SessionExpired;

    public SessionAuthService(AuthService authService, AuthSessionStore sessionStore)
    {
        _authService  = authService;
        _sessionStore = sessionStore;
    }

    /// <summary>
    /// Пробует обновить access token через refresh token.
    /// При неудаче — очищает сессию и поднимает SessionExpired.
    /// Возвращает true, если refresh прошёл успешно.
    /// </summary>
    public async Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = _sessionStore.RefreshToken;
        if (string.IsNullOrEmpty(refreshToken))
        {
            _sessionStore.Clear();
            SessionExpired?.Invoke();
            return false;
        }

        try
        {
            var newToken = await _authService.RefreshAsync(refreshToken, cancellationToken);
            if (newToken is null)
            {
                _sessionStore.Clear();
                SessionExpired?.Invoke();
                return false;
            }

            _sessionStore.ApplyRefresh(newToken);
            return true;
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _sessionStore.Clear();
            SessionExpired?.Invoke();
            return false;
        }
        catch
        {
            // Сетевые / прочие ошибки — не сбрасываем сессию, просто сообщаем о неудаче
            return false;
        }
    }

    /// <summary>
    /// Выполняет logout: вызывает backend endpoint, затем очищает локальную сессию.
    /// При сетевой ошибке всё равно очищает сессию локально.
    /// Поднимает SessionExpired — единственное место кроме TryRefreshAsync.
    /// </summary>
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var accessToken  = _sessionStore.AccessToken;
        var refreshToken = _sessionStore.RefreshToken;

        if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
        {
            try
            {
                await _authService.LogoutAsync(accessToken, refreshToken, cancellationToken);
            }
            catch
            {
                // Если backend недоступен — всё равно завершаем сессию локально
            }
        }

        _sessionStore.Clear();
        SessionExpired?.Invoke();
    }

    /// <summary>
    /// Вызывается из API-сервисов при получении 401/403.
    /// Очищает токены в store (следующий запрос с уже невалидным токеном не пройдёт),
    /// НО не поднимает SessionExpired — вызывающий код сам показывает ошибку пользователю.
    /// Используй TryRefreshAsync() если хочешь попытаться продлить сессию автоматически.
    /// </summary>
    public void HandleUnauthorized()
    {
        _sessionStore.Clear();
        // НЕ вызываем SessionExpired здесь — это приводило к принудительному
        // logout при любой 401, включая бизнес-ошибки (папка, настройки).
    }
}
