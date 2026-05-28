/// <summary>
/// HTTP-сервис для Workspace Sync API (/api/workspaces/*).
/// Следует паттерну ProjectsService:
///   - AuthorizedHttpClientFactory + HandleAuth()
///   - static JsonSerializerOptions с JsonStringEnumConverter
///   - НЕ оборачивать CreateAuthorized() в using
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
using ContourAI.Entities.Workspace;

namespace ContourAI.Shared.Api;

public sealed class WorkspaceService
{
    private readonly AuthorizedHttpClientFactory _httpFactory;
    private readonly SessionAuthService          _sessionAuthService;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public WorkspaceService(
        AuthorizedHttpClientFactory httpFactory,
        SessionAuthService          sessionAuthService)
    {
        _httpFactory        = httpFactory;
        _sessionAuthService = sessionAuthService;
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private bool HandleAuth(HttpStatusCode code)
    {
        if (code is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _sessionAuthService.HandleUnauthorized();
            return true;
        }
        return false;
    }

    // ─── POST /api/workspaces/attach ──────────────────────────────────────────

    /// <summary>
    /// Создаёт (или возвращает существующий) workspace на сервере,
    /// связывая localRootPath проекта с serverMirrorPath.
    /// </summary>
    public async Task<WorkspaceDto?> AttachAsync(
        Guid   projectId,
        string localRootPath,
        string serverMirrorPath,
        string clientInstanceId,
        CancellationToken ct = default)
    {
        var request = new AttachWorkspaceRequest(
            projectId,
            localRootPath,
            serverMirrorPath,
            clientInstanceId,
            SyncMode: 0 /* Manual */);

        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PostAsJsonAsync("api/workspaces/attach", request, JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkspaceDto>(JsonOptions, ct);
    }
    
    // ─── DELETE /api/workspaces/{id} ──────────────────────────────────────────

    /// <summary>Отвязывает workspace от сервера.</summary>
    public async Task<bool> DetachAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.DeleteAsync($"api/workspaces/{workspaceId}", ct);
        if (HandleAuth(response.StatusCode)) return false;
        return response.IsSuccessStatusCode;
    }

    // ─── GET /api/workspaces/project/{projectId} ──────────────────────────────

    /// <summary>
    /// Восстанавливает workspace проекта без повторного attach.
    /// 204 NoContent означает, что workspace для проекта не привязана.
    /// </summary>
    public async Task<WorkspaceDto?> GetByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync($"api/workspaces/project/{projectId}", ct);
        if (HandleAuth(response.StatusCode)) return null;
        if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkspaceDto>(JsonOptions, ct);
    }

    // ─── GET /api/workspaces/{id}/status ──────────────────────────────────────

    public async Task<WorkspaceDto?> GetStatusAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync($"api/workspaces/{workspaceId}/status", ct);
        if (HandleAuth(response.StatusCode)) return null;
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkspaceDto>(JsonOptions, ct);
    }

    // ─── POST /api/workspaces/{id}/snapshot ───────────────────────────────────

    /// <summary>
    /// Отправляет manifest локальных файлов на сервер.
    /// Сервер обновляет зеркало и возвращает diff-статистику.
    /// </summary>
    public async Task<SnapshotResultDto?> SnapshotAsync(
        Guid                             workspaceId,
        long                             clientRevision,
        IReadOnlyList<SnapshotFileEntry> files,
        CancellationToken                ct = default)
    {
        var request  = new SnapshotWorkspaceRequest(clientRevision, files);
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/snapshot", request, JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SnapshotResultDto>(JsonOptions, ct);
    }

    // ─── GET /api/workspaces/{id}/pending-changes ─────────────────────────────

    public async Task<PendingChangeSetsDto?> GetPendingChangesAsync(
        Guid workspaceId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync($"api/workspaces/{workspaceId}/pending-changes", ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PendingChangeSetsDto>(JsonOptions, ct);
    }

    // ─── POST /api/workspaces/{id}/apply-result ───────────────────────────────

    /// <summary>Сообщает серверу о результате применения ChangeSet клиентом.</summary>
    public async Task<bool> ReportApplyResultAsync(
        Guid                workspaceId,
        ApplyResultDto      result,
        CancellationToken   ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/apply-result", result, JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return false;
        return response.IsSuccessStatusCode;
    }

    // ─── POST /api/workspaces/{id}/agent-tasks ────────────────────────────────

    /// <summary>Запускает AgentTask с заданным промптом.</summary>
    public async Task<AgentTaskDto?> TriggerAgentTaskAsync(
        Guid              workspaceId,
        string            prompt,
        CancellationToken ct = default)
    {
        var request  = new TriggerAgentTaskRequest(prompt);
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/agent-tasks", request, JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentTaskDto>(JsonOptions, ct);
    }

    // ─── GET /api/workspaces/{id}/agent-tasks/{taskId} ────────────────────────

    public async Task<AgentTaskDto?> GetAgentTaskAsync(
        Guid workspaceId, Guid taskId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync(
            $"api/workspaces/{workspaceId}/agent-tasks/{taskId}", ct);
        if (HandleAuth(response.StatusCode)) return null;
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentTaskDto>(JsonOptions, ct);
    }

    // ─── POST /api/workspaces/{id}/agent-tasks/{taskId}/rollback ─────────────

    public async Task<bool> RollbackAgentTaskAsync(
        Guid workspaceId, Guid taskId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PostAsync(
            $"api/workspaces/{workspaceId}/agent-tasks/{taskId}/rollback",
            content: null, ct);
        if (HandleAuth(response.StatusCode)) return false;
        return response.IsSuccessStatusCode;
    }
}
