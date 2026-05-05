/// <summary>
/// DTO треда чата (ответ сервера).
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;

namespace ContourAI.Entities.Chat;

/// <param name="Id">Идентификатор треда.</param>
/// <param name="ProjectId">null для глобального треда.</param>
/// <param name="Title">Заголовок.</param>
/// <param name="MessageCount">Количество сообщений.</param>
/// <param name="LastMessageAtUtc">Дата последнего сообщения.</param>
/// <param name="CreatedAtUtc">Дата создания.</param>
public record ChatThreadDto(
    Guid      Id,
    Guid?     ProjectId,
    string    Title,
    int       MessageCount,
    DateTime? LastMessageAtUtc,
    DateTime  CreatedAtUtc)
{
    /// <summary>true — глобальный тред (без проекта).</summary>
    public bool IsGlobal => ProjectId is null;
}
