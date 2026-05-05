/// <summary>
/// Режим доступа к проекту: Private / Shared.
/// JSON: "Private" / "Shared".
/// Проект: DevAssistant / ContourAI.
/// </summary>

namespace ContourAI.Entities.Projects;

public enum ProjectAccessMode
{
    Private = 0,
    Shared  = 1
}
