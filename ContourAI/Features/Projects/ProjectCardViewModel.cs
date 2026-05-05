/// <summary>
/// ViewModel карточки проекта в списке.
/// Содержит команды Delete и OpenSettings.
/// CreatedAtLabel — форматированная дата для отображения в Sidebar.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Projects;

namespace ContourAI.Features.Projects;

public sealed partial class ProjectCardViewModel : ObservableObject
{
    public Guid              Id          { get; }
    public string            Name        { get; }
    public string            Description { get; }
    public ProjectAccessMode AccessMode  { get; }
    public DateTime          CreatedAt   { get; }
    public int               FolderCount { get; }

    public string AccessModeLabel  => AccessMode == ProjectAccessMode.Shared ? "Shared" : "Private";
    public string FolderCountLabel => FolderCount == 1 ? "1 folder" : $"{FolderCount} folders";

    /// <summary>Форматированная дата создания для Sidebar (пример: "2d ago", "May 3").</summary>
    public string CreatedAtLabel => FormatTimeAgo(CreatedAt);

    /// <summary>Открыть экран проекта (Documents / Chat).</summary>
    public event Action<ProjectCardViewModel>? OpenRequested;
    /// <summary>Открыть диалог настроек проекта.</summary>
    public event Action<ProjectCardViewModel>? SettingsRequested;
    /// <summary>Запрос на удаление проекта.</summary>
    public event Action<ProjectCardViewModel>? DeleteRequested;

    public ProjectCardViewModel(ProjectSummaryDto dto)
    {
        Id          = dto.Id;
        Name        = dto.Name;
        Description = dto.Description ?? string.Empty;
        AccessMode  = dto.AccessMode;
        CreatedAt   = dto.CreatedAtUtc;
        FolderCount = dto.FolderCount;
    }

    [RelayCommand]
    private void Open()     => OpenRequested?.Invoke(this);

    [RelayCommand]
    private void Settings() => SettingsRequested?.Invoke(this);

    [RelayCommand]
    private void Delete()   => DeleteRequested?.Invoke(this);

    private static string FormatTimeAgo(DateTime utc)
    {
        var diff = DateTime.UtcNow - utc;
        return diff switch
        {
            { TotalMinutes: < 1 }  => "just now",
            { TotalHours:   < 1 }  => $"{(int)diff.TotalMinutes}m ago",
            { TotalDays:    < 1 }  => $"{(int)diff.TotalHours}h ago",
            { TotalDays:    < 30 } => $"{(int)diff.TotalDays}d ago",
            _                      => utc.ToString("MMM d")
        };
    }
}
