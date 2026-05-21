/// <summary>
/// Краткий DTO проекта для отображения в списке.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;

namespace ContourAI.Entities.Projects;

public sealed record ProjectSummaryDto(
    Guid              Id,
    string            Name,
    string?           Description,
    ProjectAccessMode AccessMode,
    DateTime          CreatedAtUtc);
