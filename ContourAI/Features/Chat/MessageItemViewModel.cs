using System;
using ContourAI.Entities.Chat;

namespace ContourAI.Features.Chat;

[Obsolete("Use ChatMessageViewModel instead.")]
public sealed class MessageItemViewModel : ChatMessageViewModel
{
    public MessageItemViewModel(ChatMessageDto dto)
        : base(dto.Role, dto.Content, dto.CreatedAtUtc) { }

    /// <summary>Создаёт placeholder-сообщение ассистента в состоянии стриминга.</summary>
    public static MessageItemViewModel CreatePending()
        => new(new ChatMessageDto(
            Id:             Guid.NewGuid(),
            ThreadId:       Guid.Empty,
            SequenceNumber: 0,
            Role:           MessageRole.Assistant,
            Content:        string.Empty,
            TokenCount:     null,
            CreatedAtUtc:   DateTime.UtcNow))
        { IsStreaming = true };
}
