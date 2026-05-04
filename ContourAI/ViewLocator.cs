using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ContourAI.Features.Auth;

namespace ContourAI;

/// <summary>
/// Сопоставляет ViewModel с соответствующим Avalonia View.
/// Используется для ContentControl и auth-shell первой фазы UI.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        var name = param.GetType().FullName?.Replace("ViewModel", "View", StringComparison.Ordinal);
        if (name is null)
        {
            return new TextBlock { Text = "View not resolved." };
        }

        var type = Type.GetType(name);
        if (type is not null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = $"Not Found: {name}" };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase || data is MainWindowViewModel;
    }
}
