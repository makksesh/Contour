/// <summary>
/// DTO задачи индексирования.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;

namespace ContourAI.Entities.Indexing;

public sealed record IndexingTaskDto(
    Guid               Id,
    Guid               ProjectId,
    Guid               DocumentId,
    IndexingTaskStatus Status,
    int                Attempt,
    DateTime?          StartedAtUtc,
    DateTime?          CompletedAtUtc,
    string?            ErrorMessage,
    DateTime           CreatedAtUtc);
