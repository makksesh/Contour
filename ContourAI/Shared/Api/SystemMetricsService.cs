using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ContourAI.Entities.SystemMetrics;

namespace ContourAI.Shared.Api;

/// <summary>
/// Сервис чтения системных метрик с backend API.
/// GET /api/system/metrics
/// </summary>
public sealed class SystemMetricsService
{
    private readonly AuthorizedHttpClientFactory _httpFactory;
    private readonly SessionAuthService _sessionAuthService;

    public SystemMetricsService(
        AuthorizedHttpClientFactory httpFactory,
        SessionAuthService sessionAuthService)
    {
        _httpFactory = httpFactory;
        _sessionAuthService = sessionAuthService;
    }

    public async Task<SystemMetricsResponse?> GetAsync(CancellationToken ct = default)
    {
        var http = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync("api/system/metrics", ct);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _sessionAuthService.HandleUnauthorized();
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SystemMetricsResponse>(cancellationToken: ct);
    }
}
