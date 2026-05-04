/// <summary>
/// DTO и сервис dashboard для получения последних проектов, чатов и документов с сервера.
/// Контракт соответствует GET /api/dashboard/recent.
/// Проект: DevAssistant / ContourAI.
/// </summary>
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ContourAI.Entities.Dashboard;
using ContourAI.Shared.State;

namespace ContourAI.Shared.Api;

public sealed class DashboardService
{
    private readonly ConnectionSettingsStore _connectionSettingsStore;

    public DashboardService(ConnectionSettingsStore connectionSettingsStore)
    {
        _connectionSettingsStore = connectionSettingsStore;
    }

    public async Task<RecentDashboardResponse?> GetRecentAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient
        {
            BaseAddress = new Uri(_connectionSettingsStore.ServerBaseAddress, UriKind.Absolute)
        };

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.GetAsync("api/dashboard/recent", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RecentDashboardResponse>(cancellationToken: cancellationToken);
    }
}
