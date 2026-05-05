/// <summary>
/// Хранилище состояния чата.
/// Хранит выбранный тред и активный scope (Global/Project).
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.ComponentModel;
using ContourAI.Entities.Chat;

namespace ContourAI.Shared.State;

public sealed class ChatStore : INotifyPropertyChanged
{
    private Guid?    _selectedThreadId;
    private string?  _selectedThreadTitle;
    private ChatScope _activeScope = ChatScope.Global;

    public Guid?     SelectedThreadId    { get => _selectedThreadId;    private set { _selectedThreadId    = value; OnPropertyChanged(nameof(SelectedThreadId)); } }
    public string?   SelectedThreadTitle { get => _selectedThreadTitle; private set { _selectedThreadTitle = value; OnPropertyChanged(nameof(SelectedThreadTitle)); } }
    public ChatScope ActiveScope         { get => _activeScope;         private set { _activeScope         = value; OnPropertyChanged(nameof(ActiveScope)); } }

    public void SelectThread(Guid threadId, string title)
    {
        SelectedThreadId    = threadId;
        SelectedThreadTitle = title;
    }

    public void SetScope(ChatScope scope) => ActiveScope = scope;

    public void Clear()
    {
        SelectedThreadId    = null;
        SelectedThreadTitle = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
