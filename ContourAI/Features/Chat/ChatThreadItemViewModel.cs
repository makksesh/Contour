using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Chat;

namespace ContourAI.Features.Chat;

public sealed partial class ChatThreadItemViewModel : ObservableObject
{
    public Guid   Id       { get; }
    public bool   IsGlobal { get; }

    /// <summary>Отображаемое название треда.</summary>
    [ObservableProperty] private string _title = string.Empty;

    /// <summary>Время последнего сообщения (AXAML: TimeLabel).</summary>
    public string TimeLabel  { get; }

    /// <summary>Счётчик сообщений (AXAML: MessageCount). Пустая строка если 0.</summary>
    public string MessageCount { get; }

    /// <summary>Выделен ли тред как активный в списке.</summary>
    [ObservableProperty] private bool _isSelected;

    // ── Инлайн-редактирование ────────────────────────────────────────────────

    /// <summary>true — поле ввода названия видимо, TextBlock скрыт.</summary>
    [ObservableProperty] private bool   _isEditing;

    /// <summary>Буфер редактируемого названия.</summary>
    [ObservableProperty] private string _editTitle = string.Empty;

    // ── События ──────────────────────────────────────────────────────────────

    public event Action<ChatThreadItemViewModel>? Selected;
    public event Action<ChatThreadItemViewModel>? DeleteRequested;

    /// <summary>
    /// Поднимается когда пользователь подтвердил новое название.
    /// Аргумент — новая строка названия.
    /// Подписчик (ChatViewModel / AuthenticatedShellViewModel) делает PUT на сервер.
    /// </summary>
    public event Action<ChatThreadItemViewModel, string>? RenameRequested;

    // ── Конструктор ───────────────────────────────────────────────────────────

    public ChatThreadItemViewModel(ChatThreadDto dto)
    {
        Id           = dto.Id;
        _title       = dto.Title;
        IsGlobal     = dto.IsGlobal;
        TimeLabel    = FormatTimeAgo(dto.LastMessageAtUtc ?? dto.CreatedAtUtc);
        MessageCount = dto.MessageCount > 0 ? $"{dto.MessageCount} msg" : string.Empty;
    }

    // ── Команды ───────────────────────────────────────────────────────────────

    public void RaiseSelected()        => Selected?.Invoke(this);
    public void RaiseDeleteRequested() => DeleteRequested?.Invoke(this);

    /// <summary>
    /// Переключает режим редактирования.
    /// Первый клик — открывает TextBox с текущим Title.
    /// Второй клик — применяет изменения (вызывает RenameRequested).
    /// </summary>
    [RelayCommand]
    public void ToggleEdit()
    {
        if (!IsEditing)
        {
            EditTitle = Title;
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
        var newTitle = EditTitle.Trim();
        if (string.IsNullOrEmpty(newTitle)) newTitle = Title; // откат если пусто
        IsEditing = false;
        if (newTitle != Title)
            RenameRequested?.Invoke(this, newTitle);
    }

    /// <summary>Отменяет редактирование без сохранения.</summary>
    [RelayCommand]
    public void CancelEdit()
    {
        IsEditing = false;
        EditTitle = Title;
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
