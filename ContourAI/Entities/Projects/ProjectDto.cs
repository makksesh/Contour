/// <summary>
/// Полный DTO проекта, включая AccessMode и FolderCount.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;

namespace ContourAI.Entities.Projects;

public sealed record ProjectDto(
    Guid              Id,
    string            Name,
    string?           Description,
    ProjectAccessMode AccessMode,
    DateTime          CreatedAtUtc,
    int               FolderCount);
