/// <summary>
/// DTO-контракты для Workspace Sync API (/api/workspaces/*).
/// Зеркалят Application.Workspace.DTOs из LocalServerAI.
/// Проект: DevAssistant / ContourAI.
/// </summary>

namespace ContourAI.Entities.Workspace;

// ── Workspace summary ──────────────────────────────────────────────────────────

public sealed record WorkspaceDto(
    Guid              Id,
    Guid              ProjectId,
    string            ClientRootPath,
    string            ServerMirrorPath,
    WorkspaceSyncMode SyncMode,
    WorkspaceStatus   Status,
    long              LastClientRevision,
    long              LastServerRevision,
    DateTime?         LastSyncedAtUtc,
    DateTime          CreatedAtUtc);

// ── Snapshot result (server → client) ─────────────────────────────────────────

public sealed record SnapshotResultDto(
    long                   ServerRevision,
    int                    FilesAdded,
    int                    FilesUpdated,
    int                    FilesRemoved,
    IReadOnlyList<string>  ConflictingPaths);

// ── Pending changes (server → client) ─────────────────────────────────────────

public sealed record PendingChangeSetsDto(
    IReadOnlyList<ChangeSetSummaryDto> ChangeSets);

public sealed record ChangeSetSummaryDto(
    Guid            ChangeSetId,
    Guid            AgentTaskId,
    ChangeSetStatus Status,
    int             TotalFiles,
    int             AppliedFiles,
    DateTime        CreatedAtUtc);

public sealed record ChangeSetDetailDto(
    Guid                          ChangeSetId,
    Guid                          AgentTaskId,
    ChangeSetStatus               Status,
    long                          GeneratedAtServerRevision,
    IReadOnlyList<FileChangeDto>  FileChanges,
    DateTime                      CreatedAtUtc);

public sealed record FileChangeDto(
    Guid           Id,
    FileChangeType ChangeType,
    string         RelativePath,
    string?        NewRelativePath,
    /// <summary>Full content for Create/Update; null for Delete/Rename.</summary>
    string?        Content,
    string         ContentHash,
    bool           IsApplied);

// ── Apply result report (client → server) ─────────────────────────────────────

public sealed record ApplyResultDto(
    Guid                    ChangeSetId,
    IReadOnlyList<Guid>     AppliedFileChangeIds,
    IReadOnlyList<Guid>     FailedFileChangeIds);

// ── Agent task DTO ─────────────────────────────────────────────────────────────

public sealed record AgentTaskDto(
    Guid            Id,
    Guid            WorkspaceId,
    string          Prompt,
    AgentTaskStatus Status,
    long            BaseServerRevision,
    DateTime?       StartedAtUtc,
    DateTime?       FinishedAtUtc,
    string?         ErrorMessage,
    DateTime        CreatedAtUtc,
    Guid?           ChangeSetId);

// ── Request payloads (client → server) ────────────────────────────────────────

public sealed record AttachWorkspaceRequest(
    Guid   ProjectId,
    string ClientRootPath,
    string ServerMirrorPath,
    string ClientInstanceId,
    int    SyncMode);

public sealed record SnapshotFileEntry(
    string   RelativePath,
    string   MimeType,
    long     SizeBytes,
    string   ContentHash,
    DateTime ClientModifiedAtUtc,
    string?  Content);

public sealed record SnapshotWorkspaceRequest(
    long                          ClientRevision,
    IReadOnlyList<SnapshotFileEntry> Files);

public sealed record TriggerAgentTaskRequest(string Prompt);
