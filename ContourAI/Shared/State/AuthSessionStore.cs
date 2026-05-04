/// <summary>
/// Централизованное хранилище текущей пользовательской сессии.
/// Содержит AccessToken, RefreshToken, данные пользователя и флаг IsAuthenticated.
/// Все экраны, которым нужен токен, читают его отсюда — не хранят у себя.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ContourAI.Shared.Api;

namespace ContourAI.Shared.State;

public sealed class AuthSessionStore : INotifyPropertyChanged
{
    private string? _accessToken;
    private string? _refreshToken;
    private Guid _currentUserId;
    private string _currentUsername = string.Empty;
    private bool _isAuthenticated;

    /// <summary>JWT access token. Null — сессия не установлена.</summary>
    public string? AccessToken
    {
        get => _accessToken;
        private set { _accessToken = value; OnPropertyChanged(); }
    }

    /// <summary>Refresh token для обновления access token.</summary>
    public string? RefreshToken
    {
        get => _refreshToken;
        private set { _refreshToken = value; OnPropertyChanged(); }
    }

    /// <summary>ID авторизованного пользователя.</summary>
    public Guid CurrentUserId
    {
        get => _currentUserId;
        private set { _currentUserId = value; OnPropertyChanged(); }
    }

    /// <summary>Имя авторизованного пользователя для отображения в UI.</summary>
    public string CurrentUsername
    {
        get => _currentUsername;
        private set { _currentUsername = value; OnPropertyChanged(); }
    }

    /// <summary>True — пользователь авторизован и session активна.</summary>
    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        private set { _isAuthenticated = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Применяет токены и данные пользователя после успешного login/register/refresh.
    /// </summary>
    public void Apply(AuthTokenDto token)
    {
        AccessToken = token.AccessToken;
        RefreshToken = token.RefreshToken;
        CurrentUserId = token.UserId;
        CurrentUsername = token.Username;
        IsAuthenticated = true;
    }

    /// <summary>
    /// Обновляет access/refresh токены после успешного refresh-запроса.
    /// </summary>
    public void ApplyRefresh(AuthTokenDto token)
    {
        AccessToken = token.AccessToken;
        RefreshToken = token.RefreshToken;
    }

    /// <summary>
    /// Полностью очищает сессию. Вызывается при logout или 401/403.
    /// </summary>
    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        CurrentUserId = Guid.Empty;
        CurrentUsername = string.Empty;
        IsAuthenticated = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
