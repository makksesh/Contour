using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
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

    // ── auth helper ──────────────────────────────────────────────────────

    private bool HandleAuth(HttpStatusCode code)
    {
        if (code is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        { _sessionAuth.HandleUnauthorized(); return true; }
        return false;
    }

    // ── Threads ───────────────────────────────────────────────────────────

    /// <summary>GET /api/chat/projects/{projectId}/threads</summary>
    public async Task<List<ChatThreadDto>?> GetThreadsByProjectAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync($"api/chat/projects/{projectId}/threads", ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ChatThreadDto>>(JsonOptions, ct);
    }

    /// <summary>GET /api/chat/threads — глобальные треды текущего пользователя.</summary>
    public async Task<List<ChatThreadDto>?> GetGlobalThreadsAsync(CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync("api/chat/threads", ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ChatThreadDto>>(JsonOptions, ct);
    }

    /// <summary>GET /api/chat/threads/{threadId}/history</summary>
    public async Task<GetThreadHistoryResult?> GetHistoryAsync(
        Guid threadId, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.GetAsync($"api/chat/threads/{threadId}/history", ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GetThreadHistoryResult>(JsonOptions, ct);
    }

    /// <summary>POST /api/chat/threads — создать тред проекта.</summary>
    public async Task<ChatThreadDto?> CreateInProjectAsync(
        CreateThreadRequest request, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PostAsJsonAsync("api/chat/threads", request, JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatThreadDto>(JsonOptions, ct);
    }

    /// <summary>POST /api/chat/threads/global — создать глобальный тред.</summary>
    public async Task<ChatThreadDto?> CreateGlobalAsync(
        CreateGlobalThreadRequest request, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PostAsJsonAsync("api/chat/threads/global", request, JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatThreadDto>(JsonOptions, ct);
    }

    /// <summary>PUT /api/chat/threads/{threadId} — переименовать тред.</summary>
    public async Task<ChatThreadDto?> RenameAsync(
        Guid threadId, RenameThreadRequest request, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PutAsJsonAsync($"api/chat/threads/{threadId}", request, JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatThreadDto>(JsonOptions, ct);
    }

    /// <summary>POST /api/chat/threads/{threadId}/attach — привязать к проекту.</summary>
    public async Task<ChatThreadDto?> AttachToProjectAsync(
        Guid threadId, AttachThreadToProjectRequest request, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PostAsJsonAsync(
            $"api/chat/threads/{threadId}/attach", request, JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatThreadDto>(JsonOptions, ct);
    }

    /// <summary>DELETE /api/chat/threads/{threadId}/attach — отвязать от проекта.</summary>
    public async Task DetachFromProjectAsync(Guid threadId, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateAuthorized();
        await http.DeleteAsync($"api/chat/threads/{threadId}/attach", ct);
    }

    /// <summary>DELETE /api/chat/threads/{threadId} — удалить тред.</summary>
    public async Task DeleteThreadAsync(Guid threadId, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateAuthorized();
        await http.DeleteAsync($"api/chat/threads/{threadId}", ct);
    }

    // ── Messages ────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/chat/threads/{threadId}/send — синхронный ответ ассистента.
    /// </summary>
    public async Task<SendMessageResult?> SendAsync(
        Guid threadId, SendMessageRequest request, CancellationToken ct = default)
    {
        var http     = _httpFactory.CreateAuthorized();
        var response = await http.PostAsJsonAsync(
            $"api/chat/threads/{threadId}/send", request, JsonOptions, ct);
        if (HandleAuth(response.StatusCode)) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SendMessageResult>(JsonOptions, ct);
    }

    /// <summary>
    /// POST /api/chat/threads/{threadId}/stream — SSE-стриминг.
    ///
    /// Формат фреймов (НЕстандартный SSE, особенность сервера):
    ///   1. Строки с данными начинаются с пробела (БЕЗ префикса "data:"):
    ///      " {"token":"Hello"}\n\n"
    ///   2. Парсер поддерживает обе формы: с пробелом и с "data:" префиксом.
    ///   3. Завершение: фрейм с "event: done" + пустым телом.
    /// </summary>
    public async IAsyncEnumerable<string> StreamAsync(
        Guid threadId,
        SendMessageRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var http = _httpFactory.CreateAuthorized();
        var httpRequest = new HttpRequestMessage(HttpMethod.Post,
            $"api/chat/threads/{threadId}/stream")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };

        var response = await http.SendAsync(httpRequest,
            HttpCompletionOption.ResponseHeadersRead, ct);

        if (HandleAuth(response.StatusCode)) yield break;
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader       = new StreamReader(stream);

        string? eventName = null;
        string? dataLine  = null;

        /// <summary>
        /// Парсер фреймов SSE.
        /// Сервер шлёт строки в формате:
        ///   " {"token":"..."}" — пробел + JSON (БЕЗ "data:").
        ///   "data: {"token":"..."}" — стандартный SSE (тоже поддерживается).
        ///   "event: done" — завершение потока.
        ///   "" — пустая строка = разделитель фрейма.
        /// </summary>
        while (true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(ct);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            // null = поток закрыт
            if (line is null) yield break;

            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                eventName = line[6..].Trim();
            }
            else if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                // Стандартный SSE: "data: {...}"
                dataLine = line[5..].Trim();
            }
            else if (line.StartsWith(' ') || line.StartsWith('\t'))
            {
                // Нестандартный формат сервера: " {"token":"..."}"
                dataLine = line.TrimStart();
            }
            else if (line.Length == 0)
            {
                // Пустая строка — конец фрейма
                if (string.Equals(eventName, "done", StringComparison.OrdinalIgnoreCase))
                    yield break;

                if (dataLine is not null)
                {
                    string? token = TryExtractToken(dataLine);
                    if (!string.IsNullOrEmpty(token))
                        yield return token;
                }

                eventName = null;
                dataLine  = null;
            }
            // Прочие строки (comments, итд.) — пропускаем
        }
    }

    /// <summary>Извлекает поле "token" из JSON-строки. Возвращает null при ошибке парсинга.</summary>
    private static string? TryExtractToken(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("token", out var t) &&
                t.ValueKind == JsonValueKind.String)
                return t.GetString();
        }
        catch (JsonException) { /* некорректный фрейм — пропускаем */ }
        return null;
    }
}
