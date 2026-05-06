/// <summary>
/// ViewModel одного сообщения в чате.
/// Поддерживает потоковое дополнение через AppendToken (SSE-стриминг).
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ContourAI.Entities.Chat;

namespace ContourAI.Features.Chat;

public partial class ChatMessageViewModel : ObservableObject
{
    /// <summary>Роль автора: User / Assistant / System.</summary>
    public MessageRole Role { get; }

    /// <summary>Текстовое содержимое. Обновляется при стриминге токен за токеном.</summary>
    [ObservableProperty] private string _content = string.Empty;

    /// <summary>Время создания (UTC).</summary>
    public DateTime CreatedAtUtc { get; }

    /// <summary>true — сообщение сейчас генерируется (стрим не завершён).</summary>
    [ObservableProperty] private bool _isStreaming;

    public bool IsUser      => Role == MessageRole.User;
    public bool IsAssistant => Role == MessageRole.Assistant;

    public ChatMessageViewModel(MessageRole role, string content, DateTime createdAtUtc)
    {
        Role         = role;
        _content     = content;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Создаёт VM из серверного DTO.</summary>
    public static ChatMessageViewModel FromDto(ChatMessageDto dto)
        => new(dto.Role, dto.Content, dto.CreatedAtUtc);

    /// <summary>Добавляет токен при SSE-стриминге (вызывается из Dispatcher.UIThread).</summary>
    public void AppendToken(string token) => Content += token;
}
