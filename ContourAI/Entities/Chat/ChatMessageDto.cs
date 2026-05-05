/// <summary>
/// DTO сообщения чата (ответ сервера).
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;

namespace ContourAI.Entities.Chat;

/// <param name="Id">Идентификатор сообщения.</param>
/// <param name="ThreadId">Идентификатор треда.</param>
/// <param name="SequenceNumber">Порядковый номер в треде.</param>
/// <param name="Role">Роль отправителя (User / Assistant / System).</param>
/// <param name="Content">Текст сообщения.</param>
/// <param name="TokenCount">Количество токенов (null если не считалось).</param>
/// <param name="CreatedAtUtc">Дата создания.</param>
public record ChatMessageDto(
    Guid        Id,
    Guid        ThreadId,
    int         SequenceNumber,
    MessageRole Role,
    string      Content,
    int?        TokenCount,
    DateTime    CreatedAtUtc);
