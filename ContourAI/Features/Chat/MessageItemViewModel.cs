/// <summary>
/// ViewModel одного сообщения в чате.
/// Поддерживает IsStreaming — для будущего SSE-потока.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ContourAI.Entities.Chat;

namespace ContourAI.Features.Chat;

public sealed partial class MessageItemViewModel : ObservableObject
{
    public Guid     Id        { get; }
    public ChatRole Role      { get; }
    public DateTime CreatedAt { get; }

    [ObservableProperty] private string _content;
    [ObservableProperty] private bool   _isStreaming;

    public bool IsUser      => Role == ChatRole.User;
    public bool IsAssistant => Role == ChatRole.Assistant;

    public MessageItemViewModel(ChatMessageDto dto)
    {
        Id          = dto.Id;
        Role        = dto.Role;
        CreatedAt   = dto.CreatedAtUtc;
        _content    = dto.Content;
        _isStreaming = dto.IsStreaming;
    }

    /// <summary>Создаёт placeholder-сообщение ассистента во время ожидания ответа.</summary>
    public static MessageItemViewModel CreatePending()
        => new(new ChatMessageDto(
            Guid.Empty, Guid.Empty,
            ChatRole.Assistant, "...",
            DateTime.UtcNow, IsStreaming: true));
}
