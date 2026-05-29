using System;
using System.Net.Http;
using System.Net.Http.Headers;
using ContourAI.Shared.State;

namespace ContourAI.Shared.Api;

public sealed class AuthorizedHttpClientFactory : IDisposable
{
    private readonly ConnectionSettingsStore _connectionSettings;
    private readonly AuthSessionStore        _sessionStore;

    /// <summary>
    /// Единственный экземпляр HttpClient — переиспользуется для всех запросов.
    /// BaseAddress выставляется один раз при создании фабрики.
    /// </summary>
    private readonly HttpClient _authorizedClient;

    /// <summary>
    /// Анонимный клиент — для публичных эндпоинтов (login, register, health).
    /// </summary>
    private readonly HttpClient _anonymousClient;

    public AuthorizedHttpClientFactory(
        ConnectionSettingsStore connectionSettings,
        AuthSessionStore sessionStore)
    {
        _connectionSettings = connectionSettings;
        _sessionStore        = sessionStore;

        var baseUri = new Uri(_connectionSettings.ServerBaseAddress, UriKind.Absolute);

        _authorizedClient = new HttpClient { BaseAddress = baseUri };
        _anonymousClient  = new HttpClient { BaseAddress = baseUri };
    }

    /// <summary>
    /// Возвращает переиспользуемый HttpClient с актуальным Bearer-токеном.
    /// Токен читается из AuthSessionStore перед каждым вызовом,
    /// поэтому после refresh токен автоматически подхватывается.
    /// Бросает InvalidOperationException, если сессия не активна.
    /// НЕ оборачивать в using — клиент singleton и не должен Dispose-иться.
    /// </summary>
    public HttpClient CreateAuthorized()
    {
        var token = _sessionStore.AccessToken
            ?? throw new InvalidOperationException("No active session. AccessToken is null.");

        _authorizedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return _authorizedClient;
    }

    /// <summary>
    /// Возвращает переиспользуемый HttpClient без токена — для публичных эндпоинтов.
    /// НЕ оборачивать в using.
    /// </summary>
    public HttpClient CreateAnonymous() => _anonymousClient;

    public void Dispose()
    {
        _authorizedClient.Dispose();
        _anonymousClient.Dispose();
    }
}
