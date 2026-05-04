/// <summary>
/// Краткое DTO проекта для списка проектов.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;

namespace ContourAI.Entities.Projects;

public sealed record ProjectSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    DateTime UpdatedAtUtc);
