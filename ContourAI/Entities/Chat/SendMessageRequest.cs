/// <summary>
/// Запрос на отправку сообщения в тред.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;

namespace ContourAI.Entities.Chat;

public sealed record SendMessageRequest(
    Guid   ThreadId,
    string Content);
