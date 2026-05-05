/// <summary>
/// Запрос на изменение прав подключённой папки.
/// PATCH /api/projects/{projectId}/folder/permission → 204 No Content.
/// Проект: DevAssistant / ContourAI.
/// </summary>

namespace ContourAI.Entities.Projects;

public sealed record ChangeFolderPermissionRequest(FolderPermission Permission);
