using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Workspace;
using ContourAI.Shared.Client;
using ContourAI.Shared.State;

namespace ContourAI.Features.Workspace;

public sealed partial class WorkspaceSyncViewModel : ObservableObject, IDisposable
{
    private readonly LocalWorkspaceSyncService _syncService;
    private readonly WorkspaceStore            _workspaceStore;
    private CancellationTokenSource            _cts = new();
    private Timer?                             _autoSyncTimer;

    private const int AutoSyncIntervalMs = 15_000_000;

    // ── Идентификация проекта ───────────────────────────────────────────

    public Guid ProjectId { get; private set; }

    // ── Attach-форма ─────────────────────────────────────────────────────────────

    private string _localRootPath = string.Empty;

    // Серверный путь авто-генерируется из имени папки клиента.
    // Пользователь может переопределить вручную через поле.
    private string? _serverMirrorPathOverride;

    public string LocalRootPath
    {
        get => _localRootPath;
        set
        {
            if (SetProperty(ref _localRootPath, value))
                OnPropertyChanged(nameof(ServerMirrorPath));
        }
    }

    public string? ServerMirrorPathOverride
    {
        get => _serverMirrorPathOverride;
        private set => SetProperty(ref _serverMirrorPathOverride, value);
    }

    public string ServerMirrorPath
    {
        get => ServerMirrorPathOverride ?? GenerateServerPath(LocalRootPath);
        set
        {
            var auto = GenerateServerPath(LocalRootPath);
            ServerMirrorPathOverride = value == auto ? null : value;
            OnPropertyChanged();
        }
    }

    private static string GenerateServerPath(string localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath)) return string.Empty;
        var folderName = Path.GetFileName(localPath.TrimEnd(Path.DirectorySeparatorChar, '/'));
        if (string.IsNullOrEmpty(folderName)) return string.Empty;
        return $"/srv/devassistant/mirrors/{folderName}";
    }

    // ── Состояние ─────────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _isSyncing;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // ── Workspace-данные ───────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAttached))]
    [NotifyPropertyChangedFor(nameof(IsNotAttached))]
    private WorkspaceDto? _workspace;

    public bool IsAttached    => Workspace is not null && _workspaceStore.IsAttached;
    public bool IsNotAttached => !IsAttached;

    /// <summary>Кол-во ожидающих ChangeSet (pending).</summary>
    [ObservableProperty] private int _pendingChangeSetsCount;

    // ── События ────────────────────────────────────────────────────────────────

    /// <summary>Запрос на открытие панели AgentTasks.</summary>
    public event Action? NavigateToAgentTasksRequested;

    // ── Constructor ────────────────────────────────────────────────────────────

    public WorkspaceSyncViewModel(
        LocalWorkspaceSyncService syncService,
        WorkspaceStore            workspaceStore)
    {
        _syncService    = syncService;
        _workspaceStore = workspaceStore;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public async Task InitializeAsync(Guid projectId, CancellationToken ct = default)
    {
        _autoSyncTimer?.Dispose();
        _autoSyncTimer = null;
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();

        ProjectId        = projectId;
        HasError         = false;
        ErrorMessage     = string.Empty;
        StatusMessage    = string.Empty;
        Workspace         = null;
        LocalRootPath     = string.Empty;
        ServerMirrorPath  = string.Empty;

        try
        {
            var restored = await _syncService.RestoreByProjectAsync(projectId, ct);
            if (restored is null)
            {
                StatusMessage = "Рабочее пространство не подключено.";
                return;
            }

            Workspace = restored;
            LocalRootPath = restored.ClientRootPath;
            ServerMirrorPath = restored.ServerMirrorPath;

            // После восстановления подтягиваем актуальную серверную ревизию,
            // чтобы следующий snapshot шёл от свежего состояния backend.
            var freshDto = await _syncService.RefreshFromServerAsync(restored.Id, ct);
            if (freshDto is not null)
            {
                Workspace = freshDto;
                LocalRootPath = freshDto.ClientRootPath;
                ServerMirrorPath = freshDto.ServerMirrorPath;
            }

            StatusMessage = "Рабочее пространство восстановлено.";
            StartAutoSync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Открывает диалог выбора папки и устанавливает LocalRootPath.</summary>
    [RelayCommand]
    private async Task BrowseLocalPathAsync(TopLevel? topLevel)
    {
        if (topLevel is null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                    Title            = "Выберите локальную папку проекта",
                AllowMultiple    = false,
                SuggestedStartLocation = await topLevel.StorageProvider
                    .TryGetFolderFromPathAsync(
                        string.IsNullOrWhiteSpace(LocalRootPath)
                            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                            : LocalRootPath)
            });

        if (folders.Count == 0) return;

        var path = folders[0].Path.LocalPath;
        LocalRootPath = path;
        ServerMirrorPathOverride = null;
        OnPropertyChanged(nameof(ServerMirrorPath));
    }

    /// <summary>Attach: POST /api/workspaces/attach</summary>
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task AttachAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(LocalRootPath))
        {
            HasError     = true;
            ErrorMessage = "Выберите или введите путь к локальной папке проекта.";
            return;
        }

        IsLoading    = true;
        HasError     = false;
        ErrorMessage = string.Empty;
        try
        {
            var dto = await _syncService.AttachAsync(
                ProjectId, LocalRootPath.Trim(), ServerMirrorPath.Trim(), ct);

            if (dto is null)
            {
                HasError     = true;
                ErrorMessage = "Не удалось подключить рабочее пространство. Проверьте соединение и учётные данные.";
                return;
            }

            Workspace     = dto;
            StatusMessage = "Рабочее пространство успешно подключено.";
            OnPropertyChanged(nameof(IsAttached));
            OnPropertyChanged(nameof(IsNotAttached));

            // Запускаем авто-синхронизацию сразу после подключения
            StartAutoSync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = ex.Message;
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void OpenAgentTasks() => NavigateToAgentTasksRequested?.Invoke();

    // ── Auto-sync ─────────────────────────────────────────────────────────────

    private void StartAutoSync()
    {
        _autoSyncTimer?.Dispose();
        _autoSyncTimer = new Timer(
            _ => _ = ExecuteSnapshotAsync(_cts.Token),
            null,
            dueTime: TimeSpan.FromSeconds(2),
            period:  TimeSpan.FromMilliseconds(AutoSyncIntervalMs));
    }

    private async Task ExecuteSnapshotAsync(CancellationToken ct)
    {
        if (!_workspaceStore.IsAttached || !_workspaceStore.WorkspaceId.HasValue) return;
        if (IsSyncing) return; // пропускаем тик если предыдущий ещё идёт

        IsSyncing     = true;
        HasError      = false;
        StatusMessage = "Сканирование файлов…";
        try
        {
            var result = await _syncService.SnapshotAsync(
                _workspaceStore.WorkspaceId.Value,
                _workspaceStore.LocalRootPath,
                ct);

            if (result is null)
            {
                HasError     = true;
                ErrorMessage = "Не удалось отправить снимок. Сервер не ответил.";
                return;
            }

            StatusMessage =
                $"Синхронизировано — ревизия {result.ServerRevision}. " +
                $"+{result.FilesAdded} ~{result.FilesUpdated} -{result.FilesRemoved}" +
                (result.ConflictingPaths?.Count > 0
                    ? $" | конфликтов: {result.ConflictingPaths.Count}"
                    : string.Empty);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = ex.Message;
        }
        finally { IsSyncing = false; }
    }
    
    /// <summary>Detach: останавливает синк и вызывает DELETE /api/workspaces/{id}</summary>
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task DetachAsync(CancellationToken ct)
    {
        if (!_workspaceStore.IsAttached || !_workspaceStore.WorkspaceId.HasValue) return;

        IsLoading = true;
        HasError  = false;
        try
        {
            // Останавливаем таймер ДО запроса — иначе тик уйдёт параллельно с DELETE
            _autoSyncTimer?.Dispose();
            _autoSyncTimer = null;
            _cts.Cancel();

            var success = await _syncService.DetachAsync(
                _workspaceStore.WorkspaceId.Value, ct);

            if (!success)
            {
                HasError     = true;
                ErrorMessage = "Не удалось отключить рабочее пространство.";
                return;
            }

            // _workspaceStore.Clear() уже вызван внутри _syncService.DetachAsync
            // Сбрасываем только UI-состояние ViewModel
            Workspace                 = null;
            LocalRootPath             = string.Empty;
            ServerMirrorPathOverride = null;
            StatusMessage             = "Рабочее пространство отключено.";

            OnPropertyChanged(nameof(IsAttached));
            OnPropertyChanged(nameof(IsNotAttached));
            OnPropertyChanged(nameof(ServerMirrorPath));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = ex.Message;
        }
        finally { IsLoading = false; }
    }

    // ── Dispose ────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _autoSyncTimer?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
    }
}
