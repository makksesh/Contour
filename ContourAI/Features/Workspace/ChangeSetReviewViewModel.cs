/// <summary>
/// ViewModel для ревью и применения ChangeSet.
/// Показывает список файловых изменений с контентом, позволяет применить/отклонить каждый.
///
/// Поток:
///   1) LoadAsync(workspaceId, changeSetId) — GET /api/workspaces/{id}/pending-changes → detail
///   2) Пользователь смотрит FileChanges, выбирает файл → PreviewContent
///   3) ApplyAllCommand   → ChangeSetApplyService.ApplyChangeSetAsync → report → GoBack
///   4) RejectAllCommand  → (локально помечаем rejected) → сообщаем серверу
///
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Workspace;
using ContourAI.Shared.Api;
using ContourAI.Shared.Client;
using ContourAI.Shared.State;

namespace ContourAI.Features.Workspace;

// ── FileChange item VM ─────────────────────────────────────────────────────────

public sealed partial class FileChangeItemViewModel : ObservableObject
{
    public FileChangeDto Dto { get; }

    [ObservableProperty] private bool _isSelected;

    public string Icon => Dto.ChangeType switch
    {
        FileChangeType.Create => "✚",
        FileChangeType.Update => "✎",
        FileChangeType.Delete => "✕",
        FileChangeType.Rename => "→",
        _                     => "?"
    };

    public string Label => Dto.ChangeType == FileChangeType.Rename && Dto.NewRelativePath is not null
        ? $"{Dto.RelativePath} → {Dto.NewRelativePath}"
        : Dto.RelativePath;

    public bool HasContent => !string.IsNullOrEmpty(Dto.Content);

    public FileChangeItemViewModel(FileChangeDto dto)
    {
        Dto        = dto;
        IsSelected = true; // по умолчанию выбраны все
    }
}

// ── ChangeSetReviewViewModel ──────────────────────────────────────────────────

public sealed partial class ChangeSetReviewViewModel : ObservableObject
{
    private readonly WorkspaceService      _workspaceService;
    private readonly ChangeSetApplyService _applyService;
    private readonly WorkspaceStore        _workspaceStore;
    private CancellationTokenSource        _cts = new();

    // ── State ──────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _isApplying;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _successMessage = string.Empty;
    [ObservableProperty] private bool   _isApplied;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    private string? _previewContent;

    [ObservableProperty]
    private FileChangeItemViewModel? _selectedFileChange;

    public bool HasPreview => !string.IsNullOrEmpty(PreviewContent);

    public ObservableCollection<FileChangeItemViewModel> FileChanges { get; } = [];

    private ChangeSetDetailDto? _detail;

    // ── События ───────────────────────────────────────────────────────────────

    public event Action? BackRequested;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ChangeSetReviewViewModel(
        WorkspaceService      workspaceService,
        ChangeSetApplyService applyService,
        WorkspaceStore        workspaceStore)
    {
        _workspaceService = workspaceService;
        _applyService     = applyService;
        _workspaceStore   = workspaceStore;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task LoadAsync(
        Guid workspaceId,
        Guid changeSetId,
        CancellationToken ct = default)
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();

        HasError       = false;
        ErrorMessage   = string.Empty;
        SuccessMessage = string.Empty;
        IsApplied      = false;
        FileChanges.Clear();
        PreviewContent      = null;
        SelectedFileChange  = null;

        IsLoading = true;
        try
        {
            // Получаем детальный ChangeSet из pending-changes.
            // Сервер возвращает его в PendingChangeSetsDto — ищем по ID.
            var pending = await _workspaceService.GetPendingChangesAsync(workspaceId, ct);
            if (pending is null)
            {
                HasError     = true;
                ErrorMessage = "Не удалось загрузить ожидающие изменения.";
                return;
            }

            // Для детального просмотра нужен полный ChangeSet.
            // Используем ChangeSetDetailDto из списка pending (он содержит FileChanges).
            // Если сервер вернул только summary — это ограничение текущего API.
            // Пока заглушаем: детальный endpoint не реализован отдельно,
            // используем данные из ChangeSetSummaryDto.
            var summary = pending.ChangeSets.FirstOrDefault(c => c.ChangeSetId == changeSetId);
            if (summary is null)
            {
                HasError     = true;
                ErrorMessage = $"ChangeSet {changeSetId} не найден.";
                return;
            }

            // Имитируем detail из summary (файлы приходят через отдельный детальный запрос
            // когда сервер его добавит; пока используем пустой список с метаданными).
            _detail = new ChangeSetDetailDto(
                ChangeSetId:              summary.ChangeSetId,
                AgentTaskId:              summary.AgentTaskId,
                Status:                   summary.Status,
                GeneratedAtServerRevision: 0,
                FileChanges:              [],
                CreatedAtUtc:             summary.CreatedAtUtc);

            // TODO: когда добавят GET /api/workspaces/{id}/changesets/{csId}
            // вызвать его здесь вместо заглушки

            foreach (var fc in _detail.FileChanges)
                FileChanges.Add(new FileChangeItemViewModel(fc));

            OnPropertyChanged(nameof(FileChanges));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void SelectFileChange(FileChangeItemViewModel? item)
    {
        SelectedFileChange = item;
        PreviewContent     = item?.Dto.Content;
    }

    [RelayCommand]
    private void SelectAllFiles()
    {
        foreach (var fc in FileChanges) fc.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAllFiles()
    {
        foreach (var fc in FileChanges) fc.IsSelected = false;
    }

    /// <summary>
    /// Применяет выбранные изменения к локальной ФС.
    /// </summary>
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ApplyAsync(CancellationToken ct)
    {
        if (_detail is null || !_workspaceStore.IsAttached ||
            !_workspaceStore.WorkspaceId.HasValue) return;

        IsApplying   = true;
        HasError     = false;
        ErrorMessage = string.Empty;
        try
        {
            // Фильтруем только выбранные файлы
            var selectedIds = FileChanges
                .Where(fc => fc.IsSelected)
                .Select(fc => fc.Dto.Id)
                .ToHashSet();

            var filteredDetail = _detail with
            {
                FileChanges = _detail.FileChanges
                    .Where(fc => selectedIds.Contains(fc.Id))
                    .ToList()
            };

            var result = await _applyService.ApplyChangeSetAsync(
                _workspaceStore.WorkspaceId.Value,
                _workspaceStore.LocalRootPath,
                filteredDetail,
                ct);

            IsApplied = true;
            SuccessMessage =
                $"Применено файлов: {result.AppliedIds.Count}. " +
                (result.FailedIds.Count > 0 ? $"Пропущено: {result.FailedIds.Count}." : string.Empty);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsApplying = false; }
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();
}
