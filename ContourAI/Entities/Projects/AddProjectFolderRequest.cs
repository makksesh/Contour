/// <summary>
/// Запрос на подключение папки к проекту.
/// POST /api/projects/{projectId}/folders → 201 Created, FolderDto.
/// Проект: DevAssistant / ContourAI.
/// </summary>

namespace ContourAI.Entities.Projects;

public sealed record AddProjectFolderRequest(
    string           Path,
    FolderPermission Permission = FolderPermission.None);
