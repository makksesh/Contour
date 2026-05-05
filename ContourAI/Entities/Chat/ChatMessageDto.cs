namespace ContourAI.Entities.Chat;

/// <summary>DTO одного сообщения чата.</summary>
public sealed record ChatMessageDto(
    Guid     Id,
    Guid     ThreadId,
    MessageRole Role,
    string   Content,
    DateTime CreatedAtUtc);
