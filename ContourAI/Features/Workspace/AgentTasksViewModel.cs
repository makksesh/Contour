using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Workspace;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Features.Workspace;

public sealed partial class AgentTasksViewModel : ObservableObject
{
    private readonly WorkspaceService _workspaceService;
    private readonly WorkspaceStore   _workspaceStore;
    private CancellationTokenSource   _cts = new();

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(3);

    // ── State ─────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _isTriggeringTask;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage    = string.Empty;
    [ObservableProperty] private string _promptText      = string.Empty;
    [ObservableProperty] private int    _pendingCount;

    public ObservableCollection<AgentTaskViewModel> Tasks { get; } = [];

    public bool HasTasks => Tasks.Count > 0;
    public bool IsEmpty  => Tasks.Count == 0 && !IsLoading;

    // ── События ──────────────────────────────────────────────────────────────

    /// <summary>Запрос на открытие ChangeSetReviewView для конкретной задачи.</summary>
    public event Action<AgentTaskViewModel>? NavigateToReviewRequested;

    // ── Constructor ──────────────────────────────────────────────────────────

    public AgentTasksViewModel(
        WorkspaceService workspaceService,
        WorkspaceStore   workspaceStore)
    {
        _workspaceService = workspaceService;
        _workspaceStore   = workspaceStore;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();

        HasError      = false;
        ErrorMessage  = string.Empty;
        PromptText    = string.Empty;

        await LoadPendingChangesAsync(_cts.Token);
        _ = StartPollingAsync(_cts.Token);
    }

    public void Cleanup() { _cts.Cancel(); }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task TriggerTaskAsync(CancellationToken ct = default)
    {
        if (!_workspaceStore.IsAttached || !_workspaceStore.WorkspaceId.HasValue) return;
        if (string.IsNullOrWhiteSpace(PromptText)) return;

        IsTriggeringTask = true;
        HasError         = false;
        try
        {
            var dto = await _workspaceService.TriggerAgentTaskAsync(
                _workspaceStore.WorkspaceId.Value, PromptText.Trim(), ct);

            if (dto is null)
            {
                HasError     = true;
                ErrorMessage = "Не удалось создать задачу агента.";
                return;
            }

            PromptText = string.Empty;
            var taskVm = CreateTaskVm(dto);
            Tasks.Insert(0, taskVm);
            OnPropertyChanged(nameof(HasTasks));
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsTriggeringTask = false; }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        HasError     = false;
        ErrorMessage = string.Empty;
        await LoadPendingChangesAsync(_cts.Token);
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    private async Task LoadPendingChangesAsync(CancellationToken ct)
    {
        if (!_workspaceStore.IsAttached || !_workspaceStore.WorkspaceId.HasValue) return;

        IsLoading = true;
        try
        {
            var pending = await _workspaceService.GetPendingChangesAsync(
                _workspaceStore.WorkspaceId.Value, ct);

            if (pending is null) return;

            PendingCount = pending.ChangeSets.Count;
            
            Tasks.Clear();

            foreach (var cs in pending.ChangeSets)
            {
                var pseudoDto = new AgentTaskDto(
                    Id:                  cs.AgentTaskId,
                    WorkspaceId:         _workspaceStore.WorkspaceId!.Value,
                    Prompt:              "(из ChangeSet)",
                    Status:              cs.Status switch
                    {
                        ChangeSetStatus.Ready           => AgentTaskStatus.Ready,
                        ChangeSetStatus.Applied         => AgentTaskStatus.Applied,
                        ChangeSetStatus.Rejected        => AgentTaskStatus.Failed,
                        ChangeSetStatus.RolledBack      => AgentTaskStatus.RolledBack,
                        ChangeSetStatus.PartiallyApplied => AgentTaskStatus.Applied,
                        _                               => AgentTaskStatus.Pending
                    },
                    BaseServerRevision:  0,
                    StartedAtUtc:        null,
                    FinishedAtUtc:       null,
                    ErrorMessage:        null,
                    CreatedAtUtc:        cs.CreatedAtUtc,
                    ChangeSetId:         cs.ChangeSetId);

                Tasks.Add(CreateTaskVm(pseudoDto));
            }

            OnPropertyChanged(nameof(HasTasks));
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    // ── Polling ───────────────────────────────────────────────────────────────

    private async Task StartPollingAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(PollingInterval, ct).ContinueWith(_ => { });
            if (ct.IsCancellationRequested) break;

            var activeTasks = Tasks
                .Where(t => t.Status is
                    AgentTaskStatus.Pending or
                    AgentTaskStatus.Running or
                    AgentTaskStatus.GeneratingFiles)
                .ToList();

            foreach (var taskVm in activeTasks)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var updated = await _workspaceService.GetAgentTaskAsync(
                        taskVm.WorkspaceId, taskVm.TaskId, ct);
                    if (updated is not null) taskVm.Apply(updated);
                }
                catch { /* игнорируем ошибки polling */ }
            }
        }
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    private AgentTaskViewModel CreateTaskVm(AgentTaskDto dto)
    {
        var vm = new AgentTaskViewModel(dto);
        vm.ReviewRequested   += OnReviewRequested;
        vm.RollbackRequested += OnRollbackRequested;
        return vm;
    }

    private void OnReviewRequested(AgentTaskViewModel taskVm)
        => NavigateToReviewRequested?.Invoke(taskVm);

    private async void OnRollbackRequested(AgentTaskViewModel taskVm)
    {
        if (!_workspaceStore.WorkspaceId.HasValue) return;
        try
        {
            await _workspaceService.RollbackAgentTaskAsync(
                taskVm.WorkspaceId, taskVm.TaskId, _cts.Token);
            // После rollback обновляем статус
            var updated = await _workspaceService.GetAgentTaskAsync(
                taskVm.WorkspaceId, taskVm.TaskId, _cts.Token);
            if (updated is not null) taskVm.Apply(updated);
        }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
    }
}
