namespace ContourAI.Entities.Chat;

/// <summary>Ответ POST /api/chat/threads/{id}/send (non-streaming).</summary>
public sealed record SendMessageResult(
    ChatMessageDto UserMessage,
    ChatMessageDto AssistantMessage);
