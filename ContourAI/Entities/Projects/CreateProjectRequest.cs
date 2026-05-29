namespace ContourAI.Entities.Projects;

public sealed record CreateProjectRequest(
    string            Name,
    string?           Description  = null,
    ProjectAccessMode AccessMode   = ProjectAccessMode.Private);
