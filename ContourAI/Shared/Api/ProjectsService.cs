using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ContourAI.Entities.Projects;

namespace ContourAI.Shared.Api;

public sealed class ProjectsService
{
    private readonly AuthorizedHttpClientFactory _httpFactory;
    private readonly SessionAuthService          _sessionAuthService;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ProjectsService(
        AuthorizedHttpClientFactory httpFactory,
        SessionAuthService sessionAuthService)
    {
        _httpFactory        = httpFactory;
        _sessionAuthService = sessionAuthService;
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────────────────

    private bool HandleAuth(HttpStatusCode code)
    {
        if (code is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _sessionAuthService.HandleUnauthorized();
            return true;
        }
        return false;
    }

    /// <summary>Считает успехом 200 OK или 204 NoContent.</summary>
    private static bool IsSuccess(HttpStatusCode code) =>
        code == HttpStatusCode.OK || code == HttpStatusCode.NoContent;

    // ─── GET /api/projects ────────────────────────────────────────────────────────

    public async Task<List<ProjectSummaryDto>?> GetProjectsAsync(CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync("api/projects", ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ProjectSummaryDto>>(JsonOptions, ct);
    }

    // ─── GET /api/projects/{id} ───────────────────────────────────────────────────

    public async Task<ProjectDto?> GetProjectByIdAsync(Guid projectId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync($"api/projects/{projectId}", ct);
        if (HandleAuth(response.StatusCode)) return null;
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectDto>(JsonOptions, ct);
    }

    // ─── GET /api/projects/{id}/settings ────────────────────────────────────────────

    /// <summary>
    /// Возвращает текущие настройки проекта (SystemPrompt, Temperature и т.д.).
    /// Используется для пред-заполнения формы Settings перед отображением.
    /// </summary>
    public async Task<ProjectSettingsDto?> GetProjectSettingsAsync(Guid projectId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync($"api/projects/{projectId}/settings", ct);
        if (HandleAuth(response.StatusCode)) return null;
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectSettingsDto>(JsonOptions, ct);
    }

    // ─── POST /api/projects ────────────────────────────────────────────────────────

    public async Task<ProjectDto?> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PostAsJsonAsync("api/projects", request, JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectDto>(JsonOptions, ct);
    }

    // ─── DELETE /api/projects/{id} ───────────────────────────────────────────────────────

    public async Task<bool> DeleteProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.DeleteAsync($"api/projects/{projectId}", ct);
        if (HandleAuth(response.StatusCode)) return false;
        return IsSuccess(response.StatusCode);
    }

    // ─── PATCH /api/projects/{id}/settings ───────────────────────────────────────────────

    /// <summary>Обновляет настройки проекта. Считает 200 OK и 204 NoContent успехом.</summary>
    public async Task<bool> UpdateSettingsAsync(Guid projectId, UpdateProjectSettingsRequest request, CancellationToken ct = default)
    {
        var http    = _httpFactory.CreateAuthorized();
        var content = JsonContent.Create(request, options: JsonOptions);
        var response = await http.PatchAsync($"api/projects/{projectId}/settings", content, ct);
        if (HandleAuth(response.StatusCode)) return false;
        return IsSuccess(response.StatusCode);
    }

}
