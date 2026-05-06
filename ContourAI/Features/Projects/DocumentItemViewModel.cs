/// <summary>
/// ViewModel одного документа в списке вкладки Documents.
/// Хранит DocumentDto + статус задачи IndexingTaskDto (если есть).
/// IsDeleting — блокирует кнопку Delete во время запроса.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ContourAI.Entities.Documents;
using ContourAI.Entities.Indexing;

namespace ContourAI.Features.Projects;

public sealed partial class DocumentItemViewModel : ObservableObject
{
    // ─── Основные данные документа ─────────────────────────────────────────

    public Guid           Id          { get; }
    public Guid           ProjectId   { get; }
    public string         FileName    { get; }
    public string         OriginalPath { get; }
    public string?        ContentType { get; }
    public long           SizeBytes   { get; }
    public DateTime       CreatedAtUtc { get; }
    public int            ChunkCount  { get; private set; }

    // ─── Мутабельные поля ─────────────────────────────────────────────────────

    [ObservableProperty] private DocumentStatus    _docStatus;
    [ObservableProperty] private string?           _errorMessage;
    [ObservableProperty] private DateTime?         _indexedAtUtc;

    // ─── Статус индексирования ──────────────────────────────────────────────

    [ObservableProperty] private IndexingTaskStatus? _taskStatus;   // null = задачи нет
    [ObservableProperty] private Guid?               _taskId;       // для Requeue
    [ObservableProperty] private string?             _taskError;
    [ObservableProperty] private int                 _attempt;

    // ─── UI-состояния ─────────────────────────────────────────────────────────────

    [ObservableProperty] private bool _isDeleting;

    // Дисплей-свойства
    public string SizeDisplay => SizeBytes < 1_048_576
        ? $"{SizeBytes / 1024.0:F1} KB"
        : $"{SizeBytes / 1_048_576.0:F1} MB";

    public string StatusLabel => TaskStatus switch
    {
        IndexingTaskStatus.Queued    => "⏳ Queued",
        IndexingTaskStatus.Running   => "⚡ Indexing",
        IndexingTaskStatus.Completed => "✓ Indexed",
        IndexingTaskStatus.Failed    => "✗ Failed",
        null when DocStatus == DocumentStatus.Indexed => "✓ Indexed",
        _                            => "○ Uploaded"
    };

    public DocumentItemViewModel(DocumentDto dto)
    {
        Id           = dto.Id;
        ProjectId    = dto.ProjectId;
        FileName     = dto.FileName;
        OriginalPath = dto.OriginalPath;
        ContentType  = dto.ContentType;
        SizeBytes    = dto.SizeBytes;
        CreatedAtUtc = dto.CreatedAtUtc;
        ChunkCount   = dto.ChunkCount;
        _docStatus   = dto.Status;
        _errorMessage = dto.ErrorMessage;
        _indexedAtUtc = dto.IndexedAtUtc;
    }

    /// <summary>Обновляет статус задачи после очередного опроса / requeue.</summary>
    public void ApplyTask(IndexingTaskDto? task)
    {
        if (task == null)
        {
            TaskStatus = null;
            TaskId     = null;
            TaskError  = null;
            Attempt    = 0;
            return;
        }
        TaskStatus = task.Status;
        TaskId     = task.Id;
        TaskError  = task.ErrorMessage;
        Attempt    = task.Attempt;
        OnPropertyChanged(nameof(StatusLabel));
    }
}
