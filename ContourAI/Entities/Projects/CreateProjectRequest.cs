/// <summary>
/// Тело запроса POST /api/projects для создания нового проекта.
/// Проект: DevAssistant / ContourAI.
/// </summary>

namespace ContourAI.Entities.Projects;

public sealed record CreateProjectRequest(string Name, string? Description);
