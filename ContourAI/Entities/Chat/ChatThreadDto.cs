/// <summary>
/// DTO треда (диалога) чата.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;

namespace ContourAI.Entities.Chat;

public sealed record ChatThreadDto(
    Guid      Id,
    string    Title,
    ChatScope Scope,
    Guid?     ProjectId,
    string?   ProjectName,
    DateTime  CreatedAtUtc,
    DateTime  UpdatedAtUtc,
    int       MessageCount);
