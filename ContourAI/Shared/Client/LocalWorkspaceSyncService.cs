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

    // Расширения бинарных файлов, для которых Content не передаётся (только хэш)
    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".so", ".dylib", ".pdb",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp", ".tiff",
        ".mp3", ".mp4", ".wav", ".ogg", ".avi", ".mkv",
        ".zip", ".rar", ".7z", ".tar", ".gz",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx",
        ".sqlite", ".db",
    };

    // Максимальный размер файла, содержимое которого включается в снапшот (1 МБ)
    private const long MaxTextFileSizeBytes = 1 * 1024 * 1024;

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

                // Передаём текстовое содержимое файла, если:
                //   1) расширение не входит в список бинарных,
                //   2) размер файла не превышает MaxTextFileSizeBytes.
                // Для бинарных и крупных файлов Content = null — сервер учтёт их
                // только в метаданных, но не будет индексировать в RAG.
                string? content = null;
                var ext = file.Extension;
                if (!BinaryExtensions.Contains(ext) && file.Length <= MaxTextFileSizeBytes)
                {
                    try
                    {
                        content = File.ReadAllText(file.FullName, Encoding.UTF8);
                    }
                    catch (Exception readEx)
                    {
                        Console.Error.WriteLine(
                            $"[LocalWorkspaceSyncService] Cannot read text '{file.FullName}': {readEx.Message}");
                    }
                }

                entries.Add(new SnapshotFileEntry(
                    relativePath,
                    MimeType:            DetermineMimeType(ext),
                    SizeBytes:           file.Length,
                    ContentHash:         hash,
                    ClientModifiedAtUtc: file.LastWriteTimeUtc,
                    Content:             content));
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine(
                    $"[LocalWorkspaceSyncService] Skipped '{file.FullName}': {ex.Message}");
            }
        }
    }

    private static string DetermineMimeType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".cs"   => "text/x-csharp",
            ".ts"   => "text/typescript",
            ".js"   => "text/javascript",
            ".py"   => "text/x-python",
            ".java" => "text/x-java",
            ".go"   => "text/x-go",
            ".rs"   => "text/x-rust",
            ".cpp" or ".cc" or ".cxx" => "text/x-c++src",
            ".c"    => "text/x-csrc",
            ".h" or ".hpp" => "text/x-chdr",
            ".html" or ".htm" => "text/html",
            ".css"  => "text/css",
            ".json" => "application/json",
            ".xml"  => "application/xml",
            ".yaml" or ".yml" => "text/yaml",
            ".toml" => "text/toml",
            ".md"   => "text/markdown",
            ".txt"  => "text/plain",
            ".sh"   => "text/x-sh",
            ".sql"  => "application/sql",
            _       => "application/octet-stream"
        };

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexStringLower(hash)[..32];
    }
}
