/// <summary>
/// Сервис индексирования документов.
/// POST /api/indexing/queue         — поставить документ в очередь.
/// GET  /api/indexing/status/{documentId} — текущая задача. 204 = нет задачи.
/// GET  /api/indexing/queue         — весь список задач.
/// POST /api/indexing/requeue       — перепоставить задачу по taskId.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ContourAI.Entities.Indexing;

namespace ContourAI.Shared.Api;

public sealed class IndexingService
{
    private readonly AuthorizedHttpClientFactory _httpFactory;
    private readonly SessionAuthService          _sessionAuthService;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public IndexingService(
        AuthorizedHttpClientFactory httpFactory,
        SessionAuthService          sessionAuthService)
    {
        _httpFactory        = httpFactory;
        _sessionAuthService = sessionAuthService;
    }

    private bool HandleAuth(HttpStatusCode code)
    {
        if (code is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _sessionAuthService.HandleUnauthorized();
            return true;
        }
        return false;
    }

    /// <summary>POST /api/indexing/queue — поставить документ в очередь.</summary>
    public async Task<IndexingTaskDto?> QueueAsync(
        Guid documentId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PostAsJsonAsync(
            "api/indexing/queue",
            new { DocumentId = documentId },
            JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IndexingTaskDto>(JsonOptions, ct);
    }

    /// <summary>
    /// GET /api/indexing/status/{documentId}.
    /// 204 = задачи нет — возвращает null.
    /// </summary>
    public async Task<IndexingTaskDto?> GetStatusAsync(
        Guid documentId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync($"api/indexing/status/{documentId}", ct);
        if (HandleAuth(response.StatusCode)) return null;
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IndexingTaskDto>(JsonOptions, ct);
    }

    /// <summary>GET /api/indexing/queue — весь список задач.</summary>
    public async Task<List<IndexingTaskDto>?> GetQueueAsync(CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync("api/indexing/queue", ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<IndexingTaskDto>>(JsonOptions, ct);
    }

    /// <summary>POST /api/indexing/requeue — перепоставить задачу по taskId.</summary>
    public async Task<IndexingTaskDto?> RequeueAsync(
        Guid taskId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PostAsJsonAsync(
            "api/indexing/requeue",
            new { TaskId = taskId },
            JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IndexingTaskDto>(JsonOptions, ct);
    }
}
