/// <summary>
/// ViewModel одного треда в списке (левая панель чата).
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ContourAI.Entities.Chat;

namespace ContourAI.Features.Chat;

public sealed partial class ChatThreadItemViewModel : ObservableObject
{
    public Guid   Id       { get; }
    public string Title    { get; }
    public bool   IsGlobal { get; }
    public string TimeAgo  { get; }

    /// <summary>Выделен ли тред как активный в списке.</summary>
    [ObservableProperty] private bool _isSelected;

    public event Action<ChatThreadItemViewModel>? Selected;
    public event Action<ChatThreadItemViewModel>? DeleteRequested;

    public ChatThreadItemViewModel(ChatThreadDto dto)
    {
        Id       = dto.Id;
        Title    = dto.Title;
        IsGlobal = dto.IsGlobal;
        TimeAgo  = FormatTimeAgo(dto.LastMessageAtUtc ?? dto.CreatedAtUtc);
    }

    public void RaiseSelected()       => Selected?.Invoke(this);
    public void RaiseDeleteRequested() => DeleteRequested?.Invoke(this);

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
