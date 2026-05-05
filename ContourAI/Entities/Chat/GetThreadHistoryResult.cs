namespace ContourAI.Entities.Chat;

/// <summary>Ответ GET /api/chat/threads/{id}/history.</summary>
public sealed record GetThreadHistoryResult(
    Guid                  ThreadId,
    List<ChatMessageDto>  Messages);
