/// <summary>
/// ViewModel карточки проекта в списке и Sidebar.
/// Поддерживает инлайн-редактирование названия через IsEditing / EditName / ToggleEditCommand.
/// Содержит команды Delete и OpenSettings.
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
    public string            Description { get; }
    public ProjectAccessMode AccessMode  { get; }
    public DateTime          CreatedAt   { get; }
    public int               FolderCount { get; }

    /// <summary>Отображаемое название проекта.</summary>
    [ObservableProperty] private string _name = string.Empty;

    public string AccessModeLabel  => AccessMode == ProjectAccessMode.Shared ? "Shared" : "Private";
    public string FolderCountLabel => FolderCount == 1 ? "1 folder" : $"{FolderCount} folders";

    /// <summary>Форматированная дата создания для Sidebar (пример: "2d ago", "May 3").</summary>
    public string CreatedAtLabel => FormatTimeAgo(CreatedAt);

    // ── Инлайн-редактирование ────────────────────────────────────────────────

    /// <summary>true — поле ввода названия видимо, TextBlock скрыт.</summary>
    [ObservableProperty] private bool   _isEditing;

    /// <summary>Буфер редактируемого названия.</summary>
    [ObservableProperty] private string _editName = string.Empty;

    // ── События ──────────────────────────────────────────────────────────────

    public event Action<ProjectCardViewModel>? OpenRequested;
    public event Action<ProjectCardViewModel>? SettingsRequested;
    public event Action<ProjectCardViewModel>? DeleteRequested;

    /// <summary>
    /// Поднимается когда пользователь подтвердил новое название.
    /// Подписчик (ProjectsViewModel) делает PATCH на сервер.
    /// </summary>
    public event Action<ProjectCardViewModel, string>? RenameRequested;

    // ── Конструктор ───────────────────────────────────────────────────────────

    public ProjectCardViewModel(ProjectSummaryDto dto)
    {
        Id          = dto.Id;
        _name       = dto.Name;
        Description = dto.Description ?? string.Empty;
        AccessMode  = dto.AccessMode;
        CreatedAt   = dto.CreatedAtUtc;
    }

    // ── Команды ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Open()     => OpenRequested?.Invoke(this);

    [RelayCommand]
    private void Settings() => SettingsRequested?.Invoke(this);

    [RelayCommand]
    private void Delete()   => DeleteRequested?.Invoke(this);

    /// <summary>
    /// Переключает режим редактирования.
    /// Первый клик — открывает TextBox с текущим Name.
    /// Второй клик — применяет изменения (вызывает RenameRequested).
    /// </summary>
    [RelayCommand]
    public void ToggleEdit()
    {
        if (!IsEditing)
        {
            EditName  = Name;
            IsEditing = true;
        }
        else
        {
            CommitEdit();
        }
    }

    /// <summary>Подтверждает переименование и закрывает TextBox.</summary>
    [RelayCommand]
    public void CommitEdit()
    {
        if (!IsEditing) return;
        var newName = EditName.Trim();
        if (string.IsNullOrEmpty(newName)) newName = Name;
        IsEditing = false;
        if (newName != Name)
            RenameRequested?.Invoke(this, newName);
    }

    /// <summary>Отменяет редактирование без сохранения.</summary>
    [RelayCommand]
    public void CancelEdit()
    {
        IsEditing = false;
        EditName  = Name;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
