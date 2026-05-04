/// <summary>
/// Вспомогательный factory для создания HttpClient с уже подставленным Bearer-токеном.
/// Читает токен из AuthSessionStore — никакой ViewModel не нужно знать о токене напрямую.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using ContourAI.Shared.State;

namespace ContourAI.Shared.Api;

public sealed class AuthorizedHttpClientFactory
{
    private readonly ConnectionSettingsStore _connectionSettings;
    private readonly AuthSessionStore _sessionStore;

    public AuthorizedHttpClientFactory(
        ConnectionSettingsStore connectionSettings,
        AuthSessionStore sessionStore)
    {
        _connectionSettings = connectionSettings;
        _sessionStore = sessionStore;
    }

    /// <summary>
    /// Создаёт HttpClient с BaseAddress и Authorization: Bearer header.
    /// Бросает InvalidOperationException, если сессия не активна.
    /// </summary>
    public HttpClient CreateAuthorized()
    {
        var token = _sessionStore.AccessToken
            ?? throw new InvalidOperationException("No active session. AccessToken is null.");

        var client = new HttpClient
        {
            BaseAddress = new Uri(_connectionSettings.ServerBaseAddress, UriKind.Absolute)
        };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Создаёт HttpClient только с BaseAddress, без токена — для публичных эндпоинтов.
    /// </summary>
    public HttpClient CreateAnonymous()
    {
        return new HttpClient
        {
            BaseAddress = new Uri(_connectionSettings.ServerBaseAddress, UriKind.Absolute)
        };
    }
}
