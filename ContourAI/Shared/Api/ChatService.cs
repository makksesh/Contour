/// <summary>
/// Сервис для работы с чатами: треды, сообщения, отправка.
/// API:
///   GET    /api/chat/threads?scope=Global|Project&projectId={id}
///   POST   /api/chat/threads
///   DELETE /api/chat/threads/{threadId}
///   GET    /api/chat/threads/{threadId}/messages
///   POST   /api/chat/messages           (обычный ответ)
///   POST   /api/chat/messages/stream     (SSE-поток, опционально)
/// Использует AuthorizedHttpClientFactory.
/// При 401/403 — SessionAuthService.HandleUnauthorized().
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
using ContourAI.Entities.Chat;

namespace ContourAI.Shared.Api;

public sealed class ChatService
{
    private readonly AuthorizedHttpClientFactory _httpFactory;
    private readonly SessionAuthService          _sessionAuth;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ChatService(
        AuthorizedHttpClientFactory httpFactory,
        SessionAuthService          sessionAuth)
    {
        _httpFactory = httpFactory;
        _sessionAuth = sessionAuth;
    }

    private bool HandleAuth(HttpStatusCode code)
    {
        if (code is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        { _sessionAuth.HandleUnauthorized(); return true; }
        return false;
    }

    // ─── Threads ────────────────────────────────────────────────────────────

    /// <summary>Список тредов (Global или Project).</summary>
    public async Task<List<ChatThreadDto>?> GetThreadsAsync(
        ChatScope scope,
        Guid?     projectId = null,
        CancellationToken ct = default)
    {
        using var http = _httpFactory.CreateAuthorized();
        var url = $"api/chat/threads?scope={scope}";
        if (projectId.HasValue) url += $"&projectId={projectId}";

        var response = await http.GetAsync(url, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ChatThreadDto>>(JsonOptions, ct);
    }

    /// <summary>Создать новый тред.</summary>
    public async Task<ChatThreadDto?> CreateThreadAsync(
        CreateThreadRequest request,
        CancellationToken ct = default)
    {
        using var http     = _httpFactory.CreateAuthorized();
        var       response = await http.PostAsJsonAsync("api/chat/threads", request, JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatThreadDto>(JsonOptions, ct);
    }

    /// <summary>Удалить тред.</summary>
    public async Task<bool> DeleteThreadAsync(Guid threadId, CancellationToken ct = default)
    {
        using var http     = _httpFactory.CreateAuthorized();
        var       response = await http.DeleteAsync($"api/chat/threads/{threadId}", ct);
        if (HandleAuth(response.StatusCode)) return false;
        return response.StatusCode == HttpStatusCode.NoContent;
    }

    // ─── Messages ───────────────────────────────────────────────────────────

    /// <summary>История сообщений треда.</summary>
    public async Task<List<ChatMessageDto>?> GetMessagesAsync(
        Guid threadId,
        CancellationToken ct = default)
    {
        using var http     = _httpFactory.CreateAuthorized();
        var       response = await http.GetAsync($"api/chat/threads/{threadId}/messages", ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ChatMessageDto>>(JsonOptions, ct);
    }

    /// <summary>
    /// Отправить сообщение и получить ответ ассистента (non-streaming).
    /// Возвращает DTO ответного сообщения.
    /// </summary>
    public async Task<ChatMessageDto?> SendMessageAsync(
        SendMessageRequest request,
        CancellationToken ct = default)
    {
        using var http     = _httpFactory.CreateAuthorized();
        var       response = await http.PostAsJsonAsync("api/chat/messages", request, JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatMessageDto>(JsonOptions, ct);
    }
}
