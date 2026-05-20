using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using ContourAI.Features.Auth;

namespace ContourAI;

/// <summary>
/// Сопоставляет ViewModel с соответствующим Avalonia View.
/// Матчит любой тип, чьё имя заканчивается на "ViewModel" —
/// покрывает ViewModelBase (Фаза 1) и ObservableObject (Фазы 4-6).
/// Проект: DevAssistant / ContourAI.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName?.Replace("ViewModel", "View", StringComparison.Ordinal);
        if (name is null)
            return new TextBlock { Text = "Представление не найдено." };

        var type = Type.GetType(name);
        if (type is not null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = $"Не найдено представление: {name}" };
    }

    /// <summary>
    /// Принимает любой объект, чьё имя типа заканчивается на "ViewModel".
    /// Покрывает ViewModelBase (Features/Auth) и ObservableObject (CommunityToolkit)
    /// — оба базовых класса используются в проекте.
    /// </summary>
    public bool Match(object? data) =>
        data is not null &&
        data.GetType().Name.EndsWith("ViewModel", StringComparison.Ordinal);
}
