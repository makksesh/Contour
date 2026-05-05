namespace ContourAI.Entities.Chat;

/// <summary>
/// POST /api/chat/threads/{threadId}/send   — синхронный ответ.
/// POST /api/chat/threads/{threadId}/stream — SSE-стриминг.
/// </summary>
public sealed record SendMessageRequest(string Content);
