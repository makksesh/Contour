/// <summary>
/// DTO треда чата (ответ сервера).
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;

namespace ContourAI.Entities.Chat;

/// <param name="Id">Идентификатор треда.</param>
/// <param name="Title">Заголовок треда.</param>
/// <param name="IsGlobal">true — глобальный тред, false — проектный.</param>
/// <param name="ProjectId">Id проекта (null для глобального).</param>
/// <param name="MessageCount">Количество сообщений в треде.</param>
/// <param name="CreatedAtUtc">Дата создания.</param>
/// <param name="LastMessageAtUtc">Дата последнего сообщения.</param>
public record ChatThreadDto(
    Guid     Id,
    string   Title,
    bool     IsGlobal,
    Guid?    ProjectId,
    int      MessageCount,
    DateTime CreatedAtUtc,
    DateTime? LastMessageAtUtc);
