/// <summary>
/// Сервис для работы с документами: список, загрузка файла, удаление.
/// POST /api/documents/upload  — multipart/form-data (file + projectId).
/// GET  /api/documents/projects/{projectId}
/// GET  /api/documents/{documentId}
/// DELETE /api/documents/{documentId}
/// Использует AuthorizedHttpClientFactory.
/// При 401/403 — SessionAuthService.HandleUnauthorized().
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ContourAI.Entities.Documents;

namespace ContourAI.Shared.Api;

public sealed class DocumentsService
{
    private readonly AuthorizedHttpClientFactory _httpFactory;
    private readonly SessionAuthService          _sessionAuthService;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public DocumentsService(
        AuthorizedHttpClientFactory httpFactory,
        SessionAuthService sessionAuthService)
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

    // ─── GET /api/documents/projects/{projectId} ───────────────────────────────

    /// <summary>Возвращает список документов проекта.</summary>
    public async Task<List<DocumentDto>?> GetProjectDocumentsAsync(Guid projectId, CancellationToken ct = default)
    {
        using var http     = _httpFactory.CreateAuthorized();
        var       response = await http.GetAsync($"api/documents/projects/{projectId}", ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DocumentDto>>(JsonOptions, ct);
    }

    // ─── GET /api/documents/{documentId} ─────────────────────────────────────

    /// <summary>Возвращает детали одного документа.</summary>
    public async Task<DocumentDto?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
    {
        using var http     = _httpFactory.CreateAuthorized();
        var       response = await http.GetAsync($"api/documents/{documentId}", ct);
        if (HandleAuth(response.StatusCode)) return null;
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DocumentDto>(JsonOptions, ct);
    }

    // ─── POST /api/documents/upload ───────────────────────────────────────────

    /// <summary>
    /// Загружает файл на сервер (multipart/form-data).
    /// filePath — полный путь к файлу на локальной машине.
    /// </summary>
    public async Task<DocumentDto?> UploadDocumentAsync(
        Guid   projectId,
        string filePath,
        CancellationToken ct = default)
    {
        using var http    = _httpFactory.CreateAuthorized();
        await using var stream = File.OpenRead(filePath);
        var fileName = Path.GetFileName(filePath);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(projectId.ToString()),          "projectId");
        form.Add(new StreamContent(stream) { Headers = { ContentLength = stream.Length } },
                 "file", fileName);

        var response = await http.PostAsync("api/documents/upload", form, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DocumentDto>(JsonOptions, ct);
    }

    // ─── DELETE /api/documents/{documentId} ───────────────────────────────────

    /// <summary>Удаляет документ. Возвращает true при 204.</summary>
    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        using var http     = _httpFactory.CreateAuthorized();
        var       response = await http.DeleteAsync($"api/documents/{documentId}", ct);
        if (HandleAuth(response.StatusCode)) return false;
        return response.StatusCode == HttpStatusCode.NoContent;
    }
}
