namespace ContourAI.Entities.Chat;

/// <param name="UserMessage">Сохранённое сообщение пользователя.</param>
/// <param name="AssistantMessage">Ответ ассистента.</param>
public record SendMessageResult(
    ChatMessageDto UserMessage,
    ChatMessageDto AssistantMessage);
