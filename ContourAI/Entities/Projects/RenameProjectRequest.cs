namespace ContourAI.Entities.Projects;

/// <summary>
/// Лёгкий record — только новое имя.
/// AuthenticatedShellViewModel преобразует его в UpdateProjectSettingsRequest
/// (подставляя дефолты) перед вызовом PATCH /api/projects/{id}/settings.
/// </summary>
public sealed record RenameProjectRequest(string Name);
