/// <summary>
/// Сервис явного RAG-поиска по прикреплённым файлам проекта.
/// POST /api/rag/search  { projectId, query, topK? } → RagChunkDto[]
/// Используется вкладкой RagSearch для отображения релевантных чанков.
/// Чат проекта использует RAG автоматически на сервере — этот сервис
/// для явного пользовательского поиска.
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
using ContourAI.Entities.Rag;

namespace ContourAI.Shared.Api;

public sealed class RagService
{
    private readonly AuthorizedHttpClientFactory _httpFactory;
    private readonly SessionAuthService          _sessionAuthService;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public RagService(
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

    /// <summary>
    /// POST /api/rag/search — явный поиск по векторной базе проекта.
    /// Возвращает topK наиболее релевантных чанков (score ≥ 0.30).
    /// Null при ошибке авторизации.
    /// </summary>
    public async Task<List<RagChunkDto>?> SearchAsync(
        Guid              projectId,
        string            query,
        int               topK = 5,
        CancellationToken ct   = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PostAsJsonAsync(
            "api/rag/search",
            new { ProjectId = projectId, Query = query, TopK = topK },
            JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadFromJsonAsync<List<RagChunkDto>>(JsonOptions, ct);
    }
}
