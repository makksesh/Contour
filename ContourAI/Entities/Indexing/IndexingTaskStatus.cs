/// <summary>
/// Статус задачи индексирования.
/// Проект: DevAssistant / ContourAI.
/// </summary>

namespace ContourAI.Entities.Indexing;

public enum IndexingTaskStatus
{
    Queued    = 0,
    Running   = 1,
    Completed = 2,
    Failed    = 3
}
