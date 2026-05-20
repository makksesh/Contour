/// <summary>
/// Режим синхронизации workspace между клиентом и сервером.
/// Проект: DevAssistant / ContourAI.
/// </summary>
namespace ContourAI.Entities.Workspace;

public enum WorkspaceSyncMode
{
    Manual     = 0,
    OnSave     = 1,
    Continuous = 2
}
