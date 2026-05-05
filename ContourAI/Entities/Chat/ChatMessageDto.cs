/// <summary>
/// DTO одного сообщения чата.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;

namespace ContourAI.Entities.Chat;

public sealed record ChatMessageDto(
    Guid     Id,
    Guid     ThreadId,
    ChatRole Role,
    string   Content,
    DateTime CreatedAtUtc,
    bool     IsStreaming = false);
