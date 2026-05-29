using System;

namespace ContourAI.Entities.Projects;

public sealed record ProjectSummaryDto(
    Guid              Id,
    string            Name,
    string?           Description,
    ProjectAccessMode AccessMode,
    DateTime          CreatedAtUtc);
