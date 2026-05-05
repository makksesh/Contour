/// <summary>
/// Запрос на создание нового проекта.
/// POST /api/projects → 201 Created, ProjectDto.
/// Проект: DevAssistant / ContourAI.
/// </summary>

namespace ContourAI.Entities.Projects;

public sealed record CreateProjectRequest(
    string            Name,
    string?           Description  = null,
    ProjectAccessMode AccessMode   = ProjectAccessMode.Private);
