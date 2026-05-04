/// <summary>
/// Сервис для работы с проектами: список, создание, получение по ID.
/// Использует AuthorizedHttpClientFactory — токен не передаётся вручную.
/// При 401/403 вызывает SessionAuthService.HandleUnauthorized().
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ContourAI.Entities.Projects;

namespace ContourAI.Shared.Api;

public sealed class ProjectsService
{
    private readonly AuthorizedHttpClientFactory _httpFactory;
    private readonly SessionAuthService _sessionAuthService;

    public ProjectsService(
        AuthorizedHttpClientFactory httpFactory,
        SessionAuthService sessionAuthService)
    {
        _httpFactory = httpFactory;
        _sessionAuthService = sessionAuthService;
    }

    /// <summary>Возвращает список всех проектов пользователя.</summary>
    public async Task<List<ProjectSummaryDto>?> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        using var http = _httpFactory.CreateAuthorized();
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync("api/projects", cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _sessionAuthService.HandleUnauthorized();
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ProjectSummaryDto>>(cancellationToken: cancellationToken);
    }

    /// <summary>Создаёт новый проект и возвращает его DTO.</summary>
    public async Task<ProjectDto?> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        using var http = _httpFactory.CreateAuthorized();
        var response = await http.PostAsJsonAsync("api/projects", request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _sessionAuthService.HandleUnauthorized();
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectDto>(cancellationToken: cancellationToken);
    }

    /// <summary>Возвращает детали конкретного проекта по его ID.</summary>
    public async Task<ProjectDto?> GetProjectByIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        using var http = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync($"api/projects/{projectId}", cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _sessionAuthService.HandleUnauthorized();
            return null;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectDto>(cancellationToken: cancellationToken);
    }
}
