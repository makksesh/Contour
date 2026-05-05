/// <summary>
/// Обёртка над ChatMessageViewModel для обратной совместимости.
/// Проект: DevAssistant / ContourAI.
/// </summary>

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
        => new(new ChatMessageDto(Guid.NewGuid(), Guid.Empty,
            MessageRole.Assistant, string.Empty, DateTime.UtcNow))
        { IsStreaming = true };
}
