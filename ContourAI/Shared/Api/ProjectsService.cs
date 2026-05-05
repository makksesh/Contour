/// <summary>
/// Сервис для работы с проектами: список, создание, получение, удаление,
/// обновление настроек, управление папкой.
/// Использует AuthorizedHttpClientFactory — токен не передаётся вручную.
/// ВАЖНО: НЕ использовать using при вызове CreateAuthorized() — HttpClient singleton.
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

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private bool HandleAuth(HttpStatusCode code)
    {
        if (code is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _sessionAuthService.HandleUnauthorized();
            return true;
        }
        return false;
    }

    // ─── GET /api/projects ────────────────────────────────────────────────────

    /// <summary>Возвращает список всех проектов пользователя.</summary>
    public async Task<List<ProjectSummaryDto>?> GetProjectsAsync(CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync("api/projects", ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ProjectSummaryDto>>(JsonOptions, ct);
    }

    // ─── GET /api/projects/{id} ───────────────────────────────────────────────

    /// <summary>Возвращает детали конкретного проекта.</summary>
    public async Task<ProjectDto?> GetProjectByIdAsync(Guid projectId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync($"api/projects/{projectId}", ct);
        if (HandleAuth(response.StatusCode)) return null;
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectDto>(JsonOptions, ct);
    }

    // ─── POST /api/projects ───────────────────────────────────────────────────

    /// <summary>Создаёт новый проект и возвращает его DTO.</summary>
    public async Task<ProjectDto?> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PostAsJsonAsync("api/projects", request, JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectDto>(JsonOptions, ct);
    }

    // ─── DELETE /api/projects/{id} ───────────────────────────────────────────────

    /// <summary>Удаляет проект. Возвращает true при успехе (204).</summary>
    public async Task<bool> DeleteProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.DeleteAsync($"api/projects/{projectId}", ct);
        if (HandleAuth(response.StatusCode)) return false;
        return response.StatusCode == HttpStatusCode.NoContent;
    }

    // ─── PATCH /api/projects/{id}/settings ───────────────────────────────────────────

    /// <summary>Обновляет настройки проекта (модели, промпт, temperature, RAG). Возвращает true при 204.</summary>
    public async Task<bool> UpdateSettingsAsync(Guid projectId, UpdateProjectSettingsRequest request, CancellationToken ct = default)
    {
        var http    = _httpFactory.CreateAuthorized();
        var content = JsonContent.Create(request, options: JsonOptions);
        var response = await http.PatchAsync($"api/projects/{projectId}/settings", content, ct);
        if (HandleAuth(response.StatusCode)) return false;
        return response.StatusCode == HttpStatusCode.NoContent;
    }

    // ─── POST /api/projects/{id}/folders ───────────────────────────────────────────

    /// <summary>Подключает папку к проекту. Возвращает FolderDto при успехе (201).</summary>
    public async Task<FolderDto?> AddFolderAsync(Guid projectId, AddProjectFolderRequest request, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PostAsJsonAsync($"api/projects/{projectId}/folders", request, JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FolderDto>(JsonOptions, ct);
    }

    // ─── PATCH /api/projects/{id}/folder/permission ────────────────────────────────────

    /// <summary>Изменяет права подключённой папки. Возвращает true при 204.</summary>
    public async Task<bool> ChangeFolderPermissionAsync(Guid projectId, FolderPermission permission, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var request  = new ChangeFolderPermissionRequest(permission);
        var content  = JsonContent.Create(request, options: JsonOptions);
        var response = await http.PatchAsync($"api/projects/{projectId}/folder/permission", content, ct);
        if (HandleAuth(response.StatusCode)) return false;
        return response.StatusCode == HttpStatusCode.NoContent;
    }

    // ─── DELETE /api/projects/{id}/folder ─────────────────────────────────────────────

    /// <summary>Отвязывает папку от проекта. Возвращает true при 204.</summary>
    public async Task<bool> RemoveFolderAsync(Guid projectId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.DeleteAsync($"api/projects/{projectId}/folder", ct);
        if (HandleAuth(response.StatusCode)) return false;
        return response.StatusCode == HttpStatusCode.NoContent;
    }
}
