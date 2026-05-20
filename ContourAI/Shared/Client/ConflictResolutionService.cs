/// <summary>
/// Разрешает конфликты файлов при применении ChangeSet.
/// Если UserPromptDelegate не задан — используется политика KeepLocal (headless fallback).
/// UI-слой инжектирует делегат для отображения диалога пользователю.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Threading;
using System.Threading.Tasks;
using ContourAI.Entities.Workspace;

namespace ContourAI.Shared.Client;

public enum ConflictResolution { KeepLocal, TakeServer }

public enum ConflictPolicy { AskUser = 0, KeepLocal = 1, TakeServer = 2 }

public sealed record ConflictContext(
    string RelativePath,
    string LocalContent,
    string ServerContent,
    string ChangeType);

public sealed class ConflictResolutionService
{
    private ConflictPolicy _defaultPolicy = ConflictPolicy.AskUser;

    /// <summary>
    /// Делегат, показывающий diff-диалог.
    /// Если null — при AskUser политике используется KeepLocal.
    /// </summary>
    public Func<ConflictContext, CancellationToken, Task<ConflictResolution>>? UserPromptDelegate { get; set; }

    public void SetPolicy(ConflictPolicy policy) => _defaultPolicy = policy;

    public async Task<ConflictResolution> ResolveAsync(
        FileChangeDto      fileChange,
        string             existingLocalContent,
        CancellationToken  ct = default)
    {
        return _defaultPolicy switch
        {
            ConflictPolicy.KeepLocal  => ConflictResolution.KeepLocal,
            ConflictPolicy.TakeServer => ConflictResolution.TakeServer,
            ConflictPolicy.AskUser    => await AskUserAsync(fileChange, existingLocalContent, ct),
            _                         => ConflictResolution.KeepLocal
        };
    }

    private async Task<ConflictResolution> AskUserAsync(
        FileChangeDto     fileChange,
        string            existingLocalContent,
        CancellationToken ct)
    {
        if (UserPromptDelegate is null)
            return ConflictResolution.KeepLocal;

        var context = new ConflictContext(
            RelativePath:  fileChange.RelativePath,
            LocalContent:  existingLocalContent,
            ServerContent: fileChange.Content ?? string.Empty,
            ChangeType:    fileChange.ChangeType.ToString());

        return await UserPromptDelegate(context, ct);
    }
}
