// Сохранён для обратной совместимости.
// Новый код должен использовать ChatMessageViewModel.
using ContourAI.Entities.Chat;
namespace ContourAI.Features.Chat;

[Obsolete("Use ChatMessageViewModel instead.")]
public sealed class MessageItemViewModel : ChatMessageViewModel
{
    public MessageItemViewModel(ChatMessageDto dto)
        : base(dto.Role, dto.Content, dto.CreatedAtUtc) { }

    public static new MessageItemViewModel CreatePending()
        => new(new ChatMessageDto(Guid.NewGuid(), Guid.Empty,
            MessageRole.Assistant, string.Empty, DateTime.UtcNow))
        { IsStreaming = true };
}
