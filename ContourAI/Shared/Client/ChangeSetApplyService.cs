/// <summary>
/// Применяет ChangeSet к локальной файловой системе и сообщает серверу о результате.
/// Клиент — единственный владелец локальной ФС; сервер никогда не пишет файлы напрямую.
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

namespace ContourAI.Shared.Client;

// ── Result types ───────────────────────────────────────────────────────────────

public sealed record ApplySummary
{
    public int TotalChangeSets  { get; set; }
    public int TotalFilesApplied { get; set; }
    public int TotalFilesFailed  { get; set; }
}

public sealed record ApplyChangeSetResult(
    IReadOnlyList<Guid> AppliedIds,
    IReadOnlyList<Guid> FailedIds);

public sealed class ConflictException(string existingContent)
    : Exception("File conflict detected.")
{
    public string ExistingContent { get; } = existingContent;
}

// ── Service ────────────────────────────────────────────────────────────────────

public sealed class ChangeSetApplyService(
    WorkspaceService          workspaceService,
    ConflictResolutionService conflictResolver)
{
    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Применяет один ChangeSet к локальной ФС и отчитывается серверу.
    /// </summary>
    public async Task<ApplyChangeSetResult> ApplyChangeSetAsync(
        Guid                  workspaceId,
        string                localRootPath,
        ChangeSetDetailDto    changeSet,
        CancellationToken     ct = default)
    {
        var applied = new List<Guid>();
        var failed  = new List<Guid>();

        foreach (var fc in changeSet.FileChanges)
        {
            if (fc.IsApplied) continue;
            ct.ThrowIfCancellationRequested();

            try
            {
                await ApplyFileChangeAsync(localRootPath, fc, ct);
                applied.Add(fc.Id);
            }
            catch (ConflictException ex)
            {
                var resolution = await conflictResolver.ResolveAsync(fc, ex.ExistingContent, ct);
                if (resolution == ConflictResolution.TakeServer)
                {
                    await ForceApplyAsync(localRootPath, fc, ct);
                    applied.Add(fc.Id);
                }
                else
                {
                    failed.Add(fc.Id); // KeepLocal — пропускаем
                }
            }
            catch
            {
                failed.Add(fc.Id);
            }
        }

        // Сообщаем серверу
        var report = new ApplyResultDto(changeSet.ChangeSetId, applied, failed);
        await workspaceService.ReportApplyResultAsync(workspaceId, report, ct);

        return new ApplyChangeSetResult(applied, failed);
    }

    // ── File operations ────────────────────────────────────────────────────────

    private static async Task ApplyFileChangeAsync(
        string            rootPath,
        FileChangeDto     fc,
        CancellationToken ct)
    {
        var fullPath = ResolveSafe(rootPath, fc.RelativePath);

        switch (fc.ChangeType)
        {
            case FileChangeType.Create:
            case FileChangeType.Update:
                if (File.Exists(fullPath))
                {
                    var existingContent = await File.ReadAllTextAsync(fullPath, ct);
                    var existingHash    = ComputeHash(existingContent);
                    // Если хэш существующего файла отличается от ожидаемого — конфликт
                    if (existingHash != fc.ContentHash && fc.Content is not null)
                        throw new ConflictException(existingContent);
                }
                if (fc.Content is null) break;
                EnsureDirectory(fullPath);
                await File.WriteAllTextAsync(fullPath, fc.Content, Encoding.UTF8, ct);
                break;

            case FileChangeType.Delete:
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
                break;

            case FileChangeType.Rename:
                if (fc.NewRelativePath is null) break;
                var newFullPath = ResolveSafe(rootPath, fc.NewRelativePath);
                EnsureDirectory(newFullPath);
                if (File.Exists(fullPath))
                    File.Move(fullPath, newFullPath, overwrite: true);
                if (fc.Content is not null)
                    await File.WriteAllTextAsync(newFullPath, fc.Content, Encoding.UTF8, ct);
                break;
        }
    }

    private static async Task ForceApplyAsync(
        string            rootPath,
        FileChangeDto     fc,
        CancellationToken ct)
    {
        var fullPath = ResolveSafe(rootPath, fc.RelativePath);
        EnsureDirectory(fullPath);
        if (fc.Content is not null)
            await File.WriteAllTextAsync(fullPath, fc.Content, Encoding.UTF8, ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string ResolveSafe(string root, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var normalized     = relativePath.Replace('\\', '/').TrimStart('/');
        var combined       = Path.GetFullPath(Path.Combine(normalizedRoot, normalized));

        if (!combined.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path traversal: '{relativePath}'");

        return combined;
    }

    private static void EnsureDirectory(string fullPath)
    {
        var dir = Path.GetDirectoryName(fullPath);
        if (dir is not null) Directory.CreateDirectory(dir);
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
