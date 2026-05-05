/// <summary>
/// ViewModel строки треда в боковой панели.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ContourAI.Entities.Chat;

namespace ContourAI.Features.Chat;

public sealed partial class ChatThreadItemViewModel : ObservableObject
{
    public Guid   Id           { get; }
    public string Title        { get; }
    public int    MessageCount { get; }
    public string TimeLabel    => UpdatedAt.ToString("HH:mm");
    private DateTime UpdatedAt { get; }

    [ObservableProperty] private bool _isSelected;

    public event Action<ChatThreadItemViewModel>? Selected;
    public event Action<ChatThreadItemViewModel>? DeleteRequested;

    public ChatThreadItemViewModel(ChatThreadDto dto)
    {
        Id           = dto.Id;
        Title        = dto.Title;
        MessageCount = dto.MessageCount;
        UpdatedAt    = dto.UpdatedAtUtc;
    }

    public void Select()  => Selected?.Invoke(this);
    public void Delete()  => DeleteRequested?.Invoke(this);
}
