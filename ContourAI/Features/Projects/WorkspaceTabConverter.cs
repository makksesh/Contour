/// <summary>
/// Конвертеры для вкладок ProjectWorkspaceView.
/// IsSettings, IsFolder, IsDocuments, IsChat, IsSync, IsRagSearch — преобразуют int в bool.
/// IsNotNull    — true если значение не null.
/// IsTaskFailed — true если IndexingTaskStatus == Failed.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using ContourAI.Entities.Indexing;

namespace ContourAI.Features.Projects;

/// <summary>Проверяет int == _tab.</summary>
public sealed class IsTabConverter : IValueConverter
{
    private readonly int _tab;
    public IsTabConverter(int tab) => _tab = tab;
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => v is int i && i == _tab;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>true если значение != null.</summary>
public sealed class NotNullConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is not null;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>true если IndexingTaskStatus? == Failed.</summary>
public sealed class TaskFailedConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => v is IndexingTaskStatus s && s == IndexingTaskStatus.Failed;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>Статические синглтоны — используются в AXAML через {x:Static}.</summary>
public static class WorkspaceTabConverter
{
    public static readonly IsTabConverter    IsSettings  = new((int)WorkspaceTab.Settings);
    public static readonly IsTabConverter    IsFolder    = new((int)WorkspaceTab.Folder);
    public static readonly IsTabConverter    IsDocuments = new((int)WorkspaceTab.Documents);
    public static readonly IsTabConverter    IsChat      = new((int)WorkspaceTab.Chat);
    public static readonly IsTabConverter    IsSync      = new((int)WorkspaceTab.Sync);
    public static readonly IsTabConverter    IsRagSearch = new((int)WorkspaceTab.RagSearch);
    public static readonly NotNullConverter  IsNotNull   = new();
    public static readonly TaskFailedConverter IsTaskFailed = new();
}
