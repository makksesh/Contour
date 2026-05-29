using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ContourAI.Shared.State;

namespace ContourAI.Shared.Api;

public sealed class AuthService
{
    private readonly ConnectionSettingsStore _connectionSettingsStore;

    public AuthService(ConnectionSettingsStore connectionSettingsStore)
    {
        _connectionSettingsStore = connectionSettingsStore;
    }

    public async Task<AuthTokenDto?> RegisterAsync(string username, string email, string password, CancellationToken cancellationToken = default)
    {
        using var http = CreateHttpClient();
        var request = new RegisterRequest(username, email, password);
        using var response = await http.PostAsJsonAsync("api/auth/register", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthTokenDto>(cancellationToken: cancellationToken);
    }

    public async Task<AuthTokenDto?> LoginAsync(string identifier, string password, CancellationToken cancellationToken = default)
    {
        using var http = CreateHttpClient();
        var request = new LoginRequest(identifier, password);
        using var response = await http.PostAsJsonAsync("api/auth/login", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthTokenDto>(cancellationToken: cancellationToken);
    }

    public async Task<AuthTokenDto?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        using var http = CreateHttpClient();
        var request = new RefreshTokenRequest(refreshToken);
        using var response = await http.PostAsJsonAsync("api/auth/refresh", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthTokenDto>(cancellationToken: cancellationToken);
    }

    public async Task LogoutAsync(string accessToken, string refreshToken, CancellationToken cancellationToken = default)
    {
        using var http = CreateHttpClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var request = new LogoutRequest(refreshToken);
        using var response = await http.PostAsJsonAsync("api/auth/logout", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<UserDto?> GetMeAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var http = CreateHttpClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.GetAsync("api/auth/me", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserDto>(cancellationToken: cancellationToken);
    }

    private HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            BaseAddress = new Uri(_connectionSettingsStore.ServerBaseAddress, UriKind.Absolute)
        };
    }
}

public sealed record AuthTokenDto(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc,
    DateTime RefreshTokenExpiresAtUtc,
    Guid UserId,
    string Username);

public sealed record UserDto(
    Guid Id,
    string Username,
    string Email,
    DateTime CreatedAtUtc);

public sealed record RegisterRequest(string Username, string Email, string Password);

public sealed record LoginRequest(string Identifier, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);
