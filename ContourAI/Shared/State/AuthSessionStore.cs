/// <summary>
/// Централизованное хранилище текущей пользовательской сессии.
/// Содержит AccessToken, RefreshToken, данные пользователя и флаг IsAuthenticated.
/// Все экраны, которым нужен токен, читают его отсюда — не хранят у себя.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ContourAI.Shared.Api;

namespace ContourAI.Shared.State;

public sealed class AuthSessionStore : INotifyPropertyChanged
{
    private string? _accessToken;
    private string? _refreshToken;
    private Guid _currentUserId;
    private string _currentUsername = string.Empty;
    private string _currentUserRole = string.Empty;
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

    /// <summary>Роль текущего пользователя, извлечённая из JWT claim `role`.</summary>
    public string CurrentUserRole
    {
        get => _currentUserRole;
        private set
        {
            _currentUserRole = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAdmin));
        }
    }

    public bool IsAdmin => string.Equals(CurrentUserRole, "Admin", StringComparison.OrdinalIgnoreCase);

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
        CurrentUserRole = ExtractRole(token.AccessToken);
        IsAuthenticated = true;
    }

    /// <summary>
    /// Обновляет access/refresh токены после успешного refresh-запроса.
    /// </summary>
    public void ApplyRefresh(AuthTokenDto token)
    {
        AccessToken = token.AccessToken;
        RefreshToken = token.RefreshToken;
        CurrentUserRole = ExtractRole(token.AccessToken);
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
        CurrentUserRole = string.Empty;
        IsAuthenticated = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static string ExtractRole(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return string.Empty;

        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length < 2)
                return string.Empty;

            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');

            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (TryReadRole(root, "role", out var role) ||
                TryReadRole(root, "http://schemas.microsoft.com/ws/2008/06/identity/claims/role", out role))
                return role;
        }
        catch
        {
            // Ignore malformed tokens; role-gated UI will stay disabled.
        }

        return string.Empty;
    }

    private static bool TryReadRole(JsonElement root, string propertyName, out string role)
    {
        role = string.Empty;

        if (!root.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.String)
        {
            role = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(role);
        }

        if (property.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in property.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                    continue;

                role = item.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(role))
                    return true;
            }
        }

        return false;
    }
}
