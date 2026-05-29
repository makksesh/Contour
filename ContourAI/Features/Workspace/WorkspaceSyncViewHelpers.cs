using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ContourAI.Features.Workspace;

/// <summary>true если строка не null и не пустая.</summary>
public sealed class NonEmptyStringConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => v is string s && !string.IsNullOrEmpty(s);
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>true если строка null или пустая (инверсия NonEmptyStringConverter).</summary>
public sealed class EmptyStringConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => v is not string s || string.IsNullOrEmpty(s);
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>true если bool == false (инверсия для IsEnabled).</summary>
public sealed class NotBoolConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => v is bool b && !b;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>Статические синглтоны — используются через {x:Static}.</summary>
public static class WorkspaceSyncViewHelpers
{
    public static readonly NonEmptyStringConverter IsNonEmpty = new();
    public static readonly EmptyStringConverter    IsEmpty    = new();
    public static readonly NotBoolConverter        NotBool    = new();
}

/// <summary>
/// Конвертер SyncSubPanelIndex (int) → bool для внутренней навигации вкладки Sync.
/// 0=Sync, 1=AgentTasks, 2=Review.
/// </summary>
public sealed class SyncSubPanelIndexConverter : IValueConverter
{
    private readonly int _panel;
    public SyncSubPanelIndexConverter(int panel) => _panel = panel;
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => v is int i && i == _panel;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>Статические синглтоны для SyncSubPanelIndex.</summary>
public static class SyncSubPanelConverter
{
    public static readonly SyncSubPanelIndexConverter IsSync       = new(0);
    public static readonly SyncSubPanelIndexConverter IsAgentTasks = new(1);
    public static readonly SyncSubPanelIndexConverter IsReview     = new(2);
}
