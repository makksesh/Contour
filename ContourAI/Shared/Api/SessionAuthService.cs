/// <summary>
/// Сервис управления session-lifecycle: refresh токена и logout с очисткой состояния.
/// Использует AuthSessionStore как единый источник истины о текущей сессии.
/// Централизованно обрабатывает 401/403 — сбрасывает сессию и инициирует redirect на login.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ContourAI.Shared.State;

namespace ContourAI.Shared.Api;

public sealed class SessionAuthService
{
    private readonly AuthService _authService;
    private readonly AuthSessionStore _sessionStore;

    /// <summary>
    /// Поднимается, когда 401/403 или явный logout — подписывается MainWindowViewModel.
    /// </summary>
    public event Action? SessionExpired;

    public SessionAuthService(AuthService authService, AuthSessionStore sessionStore)
    {
        _authService = authService;
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
            InvalidateSession();
            return false;
        }

        try
        {
            var newToken = await _authService.RefreshAsync(refreshToken, cancellationToken);
            if (newToken is null)
            {
                InvalidateSession();
                return false;
            }

            _sessionStore.ApplyRefresh(newToken);
            return true;
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            InvalidateSession();
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
    /// </summary>
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = _sessionStore.AccessToken;
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

        InvalidateSession();
    }

    /// <summary>
    /// Должен вызываться из любого места, получившего 401 или 403 от API.
    /// Очищает сессию и уведомляет подписчиков о необходимости redirect на login.
    /// </summary>
    public void HandleUnauthorized()
    {
        InvalidateSession();
    }

    private void InvalidateSession()
    {
        _sessionStore.Clear();
        SessionExpired?.Invoke();
    }
}
