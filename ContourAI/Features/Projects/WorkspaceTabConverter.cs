/// <summary>
/// Конвертеры для проверки активной вкладки в ProjectWorkspaceView.
/// Используются в XAML через Classes.active и IsVisible.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ContourAI.Features.Projects;

/// <summary>
/// Синглтон-конвертер для каждой вкладки: int -> bool.
/// </summary>
public sealed class WorkspaceTabIndexConverter : IValueConverter
{
    private readonly int _tabIndex;

    public WorkspaceTabIndexConverter(int tabIndex) => _tabIndex = tabIndex;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int idx && idx == _tabIndex;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Статические экземпляры конвертеров, подключаемых в XAML через x:Static.
/// </summary>
public static class WorkspaceTabConverter
{
    public static readonly IValueConverter IsSettings  = new WorkspaceTabIndexConverter((int)WorkspaceTab.Settings);
    public static readonly IValueConverter IsFolder    = new WorkspaceTabIndexConverter((int)WorkspaceTab.Folder);
    public static readonly IValueConverter IsDocuments = new WorkspaceTabIndexConverter((int)WorkspaceTab.Documents);
    public static readonly IValueConverter IsChat      = new WorkspaceTabIndexConverter((int)WorkspaceTab.Chat);
}

/// <summary>
/// Конвертер «значение != null → true» для IsVisible на вложенном DataContext.
/// </summary>
public sealed class NotNullConverter : IValueConverter
{
    public static readonly NotNullConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
