/// <summary>
/// DTO подключённой папки проекта.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;

namespace ContourAI.Entities.Projects;

public sealed record FolderDto(
    Guid             Id,
    Guid             ProjectId,
    string           Path,
    FolderPermission Permission);
