using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Workspace;

namespace ContourAI.Features.Workspace;

public sealed partial class AgentTaskViewModel : ObservableObject
{
    // ── Данные задачи ────────────────────────────────────────────────────────

    public Guid            TaskId         { get; }
    public Guid            WorkspaceId    { get; }
    public string          Prompt         { get; }
    public DateTime        CreatedAtUtc   { get; }
    public Guid?           ChangeSetId    { get; }

    [ObservableProperty] private AgentTaskStatus _status;
    [ObservableProperty] private string          _statusLabel = string.Empty;
    [ObservableProperty] private string?         _errorMessage;
    [ObservableProperty] private bool            _isRollingBack;

    /// <summary>True если задача завершилась успешно и есть ChangeSet для ревью.</summary>
    public bool CanReview => Status is AgentTaskStatus.Ready or AgentTaskStatus.Applied
                             && ChangeSetId.HasValue;

    public bool CanRollback => Status == AgentTaskStatus.Applied;
    public bool IsTerminal  => Status is AgentTaskStatus.Applied
                                      or AgentTaskStatus.Failed
                                      or AgentTaskStatus.RolledBack;

    // ── Событие открытия ревью ────────────────────────────────────────────────

    public event Action<AgentTaskViewModel>? ReviewRequested;
    public event Action<AgentTaskViewModel>? RollbackRequested;

    // ── Constructor ──────────────────────────────────────────────────────────

    public AgentTaskViewModel(AgentTaskDto dto)
    {
        TaskId       = dto.Id;
        WorkspaceId  = dto.WorkspaceId;
        Prompt       = dto.Prompt;
        CreatedAtUtc = dto.CreatedAtUtc;
        ChangeSetId  = dto.ChangeSetId;
        Apply(dto);
    }

    // ── Обновление из DTO (при polling) ──────────────────────────────────────

    public void Apply(AgentTaskDto dto)
    {
        Status       = dto.Status;
        ErrorMessage = dto.ErrorMessage;
        StatusLabel  = dto.Status switch
        {
            AgentTaskStatus.Pending         => "⏳ Pending",
            AgentTaskStatus.Running         => "🔄 Running",
            AgentTaskStatus.GeneratingFiles => "📝 Generating files",
            AgentTaskStatus.Ready           => "✅ Ready",
            AgentTaskStatus.Applied         => "✔ Applied",
            AgentTaskStatus.Failed          => "✗ Failed",
            AgentTaskStatus.RolledBack      => "⏪ Rolled back",
            _                               => dto.Status.ToString()
        };
        OnPropertyChanged(nameof(CanReview));
        OnPropertyChanged(nameof(CanRollback));
        OnPropertyChanged(nameof(IsTerminal));
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenReview() => ReviewRequested?.Invoke(this);

    [RelayCommand]
    private void RequestRollback() => RollbackRequested?.Invoke(this);
}
