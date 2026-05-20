/// <summary>
/// Тип операции над файлом в ChangeSet.
/// Проект: DevAssistant / ContourAI.
/// </summary>
namespace ContourAI.Entities.Workspace;

public enum FileChangeType
{
    Create = 0,
    Update = 1,
    Delete = 2,
    Rename = 3
}
