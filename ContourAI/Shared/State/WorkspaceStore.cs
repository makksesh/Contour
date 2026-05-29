using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ContourAI.Entities.Workspace;

namespace ContourAI.Shared.State;

public sealed class WorkspaceStore : INotifyPropertyChanged
{
    private Guid?   _workspaceId;
    private string  _localRootPath   = string.Empty;
    private string  _serverMirrorPath = string.Empty;
    private long    _lastServerRevision;
    private WorkspaceStatus   _status = WorkspaceStatus.Inactive;
    private WorkspaceSyncMode _syncMode = WorkspaceSyncMode.Manual;

    // ── Public properties ─────────────────────────────────────────────────────

    /// <summary>ID текущего workspace на сервере. Null если не подключён.</summary>
    public Guid? WorkspaceId
    {
        get => _workspaceId;
        private set { _workspaceId = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAttached)); }
    }

    /// <summary>Абсолютный путь к локальной папке проекта на клиенте.</summary>
    public string LocalRootPath
    {
        get => _localRootPath;
        private set { _localRootPath = value; OnPropertyChanged(); }
    }

    /// <summary>Путь к зеркальной копии на сервере.</summary>
    public string ServerMirrorPath
    {
        get => _serverMirrorPath;
        private set { _serverMirrorPath = value; OnPropertyChanged(); }
    }

    /// <summary>Последняя известная серверная ревизия.</summary>
    public long LastServerRevision
    {
        get => _lastServerRevision;
        set { _lastServerRevision = value; OnPropertyChanged(); }
    }

    /// <summary>Статус workspace (Active, Syncing, Error…).</summary>
    public WorkspaceStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    /// <summary>Режим синхронизации.</summary>
    public WorkspaceSyncMode SyncMode
    {
        get => _syncMode;
        set { _syncMode = value; OnPropertyChanged(); }
    }

    /// <summary>True если workspace подключён (WorkspaceId != null).</summary>
    public bool IsAttached => _workspaceId.HasValue;

    // ── Mutators ──────────────────────────────────────────────────────────────

    /// <summary>Применяет данные из WorkspaceDto после успешного Attach/GetStatus.</summary>
    public void Apply(WorkspaceDto dto)
    {
        WorkspaceId        = dto.Id;
        LocalRootPath      = dto.ClientRootPath;
        ServerMirrorPath   = dto.ServerMirrorPath;
        LastServerRevision = dto.LastServerRevision;
        Status             = dto.Status;
        SyncMode           = dto.SyncMode;
    }

    /// <summary>Сбрасывает store при закрытии проекта или logout.</summary>
    public void Clear()
    {
        WorkspaceId        = null;
        LocalRootPath      = string.Empty;
        ServerMirrorPath   = string.Empty;
        LastServerRevision = 0;
        Status             = WorkspaceStatus.Inactive;
        SyncMode           = WorkspaceSyncMode.Manual;
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    

}
