/// <summary>
/// Область чата: Global (без привязки к проекту) или Project (в контексте проекта).
/// Проект: DevAssistant / ContourAI.
/// </summary>

namespace ContourAI.Entities.Chat;

public enum ChatScope
{
    Global  = 0,
    Project = 1
}
