/// <summary>
/// Сканирует локальную ФС, вычисляет SHA-256 для каждого файла и отправляет
/// snapshot на сервер. Поддерживает FileSystemWatcher с debounce для автосинка.
/// Исключает .git, bin, obj, node_modules, venv, dist и т.д.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ContourAI.Entities.Workspace;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Shared.Client;

public sealed class LocalWorkspaceSyncService : IDisposable
{
    // Папки, исключаемые из синхронизации
    private static readonly HashSet<string> ExcludedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", "node_modules", "venv", ".venv", "dist",
        "__pycache__", ".vs", ".idea", ".workspace-backup"
    };

    // Стабильный ID клиента — читается из переменной окружения или генерируется один раз
    private static readonly string ClientInstanceId =
        Environment.GetEnvironmentVariable("DEVASSISTANT_CLIENT_ID")
        ?? Guid.NewGuid().ToString();

    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(500);

    private readonly WorkspaceService  _workspaceService;
    private readonly WorkspaceStore    _workspaceStore;

    // Активные watchers: workspaceId → (watcher, debounce timer)
    private readonly Dictionary<Guid, (FileSystemWatcher Watcher, Timer Debounce)> _watchers = [];
    private readonly Lock _watcherLock = new();

    public LocalWorkspaceSyncService(
        WorkspaceService workspaceService,
        WorkspaceStore   workspaceStore)
    {
        _workspaceService = workspaceService;
        _workspaceStore   = workspaceStore;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Подключает рабочее пространство проекта к серверу.
    /// Обновляет WorkspaceStore после успешного Attach.
    /// </summary>
    public async Task<WorkspaceDto?> AttachAsync(
        Guid              projectId,
        string            localRootPath,
        string            serverMirrorPath,
        CancellationToken ct = default)
    {
        var dto = await _workspaceService.AttachAsync(
            projectId, localRootPath, serverMirrorPath, ClientInstanceId, ct);

        if (dto is not null)
            _workspaceStore.Apply(dto);

        return dto;
    }

    /// <summary>
    /// Сканирует локальную ФС и отправляет snapshot на сервер.
    /// Обновляет LastServerRevision в WorkspaceStore.
    /// </summary>
    public async Task<SnapshotResultDto?> SnapshotAsync(
        Guid              workspaceId,
        string            localRootPath,
        CancellationToken ct = default)
    {
        var entries = await ScanDirectoryAsync(localRootPath, ct);

        var result = await _workspaceService.SnapshotAsync(
            workspaceId,
            _workspaceStore.LastServerRevision,
            entries,
            ct);

        if (result is not null)
            _workspaceStore.LastServerRevision = result.ServerRevision;

        return result;
    }

    /// <summary>
    /// Запускает FileSystemWatcher с debounce.
    /// При изменении файлов автоматически вызывает SnapshotAsync.
    /// </summary>
    public void StartWatching(Guid workspaceId, string localRootPath)
    {
        lock (_watcherLock)
        {
            if (_watchers.ContainsKey(workspaceId)) return;

            var watcher = new FileSystemWatcher(localRootPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            var timer = new Timer(
                _ => _ = OnDebouncedChangeAsync(workspaceId, localRootPath),
                state:   null,
                dueTime: Timeout.InfiniteTimeSpan,
                period:  Timeout.InfiniteTimeSpan);

            void ResetTimer(object? _, FileSystemEventArgs __) =>
                timer.Change(DebounceDelay, Timeout.InfiniteTimeSpan);

            watcher.Changed += ResetTimer;
            watcher.Created += ResetTimer;
            watcher.Deleted += ResetTimer;
            watcher.Renamed += (_, __) => timer.Change(DebounceDelay, Timeout.InfiniteTimeSpan);

            _watchers[workspaceId] = (watcher, timer);
        }
    }

    /// <summary>Останавливает FileSystemWatcher для указанного workspace.</summary>
    public void StopWatching(Guid workspaceId)
    {
        lock (_watcherLock)
        {
            if (!_watchers.TryGetValue(workspaceId, out var entry)) return;
            entry.Watcher.EnableRaisingEvents = false;
            entry.Watcher.Dispose();
            entry.Debounce.Dispose();
            _watchers.Remove(workspaceId);
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        lock (_watcherLock)
        {
            foreach (var (_, entry) in _watchers)
            {
                entry.Watcher.Dispose();
                entry.Debounce.Dispose();
            }
            _watchers.Clear();
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task OnDebouncedChangeAsync(Guid workspaceId, string localRootPath)
    {
        try { await SnapshotAsync(workspaceId, localRootPath); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LocalWorkspaceSyncService] Debounced snapshot failed: {ex.Message}");
        }
    }

    private static async Task<IReadOnlyList<SnapshotFileEntry>> ScanDirectoryAsync(
        string            rootPath,
        CancellationToken ct)
    {
        var entries = new List<SnapshotFileEntry>();
        var root    = new DirectoryInfo(rootPath);

        if (!root.Exists)
            throw new DirectoryNotFoundException($"Локальный корневой каталог не найден: '{rootPath}'");

        await Task.Run(() => ScanDirectory(root, root, entries, ct), ct);
        return entries.AsReadOnly();
    }

    private static void ScanDirectory(
        DirectoryInfo           root,
        DirectoryInfo           dir,
        List<SnapshotFileEntry> entries,
        CancellationToken       ct)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var subDir in dir.EnumerateDirectories())
        {
            if (ExcludedDirs.Contains(subDir.Name)) continue;
            ScanDirectory(root, subDir, entries, ct);
        }

        foreach (var file in dir.EnumerateFiles())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var relativePath = Path.GetRelativePath(root.FullName, file.FullName)
                    .Replace(Path.DirectorySeparatorChar, '/');

                var hash = ComputeSha256(file.FullName);

                // Content передаём null при первом сканировании для экономии трафика;
                // сервер запросит полный контент только для изменённых файлов через diff.
                entries.Add(new SnapshotFileEntry(
                    relativePath,
                    MimeType:           "application/octet-stream",
                    SizeBytes:          file.Length,
                    ContentHash:        hash,
                    ClientModifiedAtUtc: file.LastWriteTimeUtc,
                    Content:            null));
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine(
                    $"[LocalWorkspaceSyncService] Skipped '{file.FullName}': {ex.Message}");
            }
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexStringLower(hash)[..32];
    }
}
