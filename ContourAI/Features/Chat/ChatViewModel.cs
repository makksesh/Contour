/// <summary>
/// ViewModel экрана чата.
/// Управляет списком тредов, историей сообщений и отправкой.
/// scope = Global  → обычный чат с AI без контекста проекта.
/// scope = Project → чат с RAG-контекстом выбранного проекта.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Chat;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Features.Chat;

public sealed partial class ChatViewModel : ObservableObject
{
    private readonly ChatService         _chatService;
    private readonly ChatStore           _chatStore;
    private readonly ProjectContextStore _projectContext;

    public ObservableCollection<ChatThreadItemViewModel> Threads  { get; } = new();
    public ObservableCollection<MessageItemViewModel>    Messages { get; } = new();

    // ─── Scope ──────────────────────────────────────────────────────────────
    [ObservableProperty] private ChatScope _activeScope = ChatScope.Global;
    [ObservableProperty] private string    _scopeLabel  = "Global Chat";

    partial void OnActiveScopeChanged(ChatScope value)
    {
        ScopeLabel = value == ChatScope.Global ? "Global Chat" : "Project Chat";
        _ = LoadThreadsAsync();
    }

    // ─── Selected thread ────────────────────────────────────────────────────
    [ObservableProperty] private ChatThreadItemViewModel? _selectedThread;
    [ObservableProperty] private string _selectedThreadTitle = "Select or create a thread";

    partial void OnSelectedThreadChanged(ChatThreadItemViewModel? value)
    {
        if (value == null) return;
        SelectedThreadTitle = value.Title;
        _chatStore.SelectThread(value.Id, value.Title);
        _ = LoadMessagesAsync(value.Id);
    }

    // ─── Input ──────────────────────────────────────────────────────────────
    [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty] private bool _isSending;
    [ObservableProperty] private bool _isLoadingMessages;
    [ObservableProperty] private bool _isLoadingThreads;

    // ─── States ─────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool   _isThreadsEmpty;
    [ObservableProperty] private bool   _isMessagesEmpty;

    // ─── New thread dialog ──────────────────────────────────────────────────
    [ObservableProperty] private bool   _isNewThreadDialogOpen;
    [ObservableProperty] private string _newThreadTitle = string.Empty;

    private CancellationTokenSource? _sendCts;

    public ChatViewModel(
        ChatService         chatService,
        ChatStore           chatStore,
        ProjectContextStore projectContext)
    {
        _chatService    = chatService;
        _chatStore      = chatStore;
        _projectContext = projectContext;
    }

    // ─── Initialize ─────────────────────────────────────────────────────────

    public async Task InitializeAsync(ChatScope scope = ChatScope.Global)
    {
        ActiveScope = scope;
        await LoadThreadsAsync();
    }

    // ─── Load threads ────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadThreadsAsync()
    {
        IsLoadingThreads = true;
        HasError         = false;
        Threads.Clear();
        Messages.Clear();
        SelectedThread  = null;
        IsMessagesEmpty = true;

        try
        {
            var projectId = ActiveScope == ChatScope.Project
                ? (Guid?)_projectContext.SelectedProjectId
                : null;

            var list = await _chatService.GetThreadsAsync(ActiveScope, projectId);
            if (list == null) return;

            foreach (var dto in list)
            {
                var item = new ChatThreadItemViewModel(dto);
                item.Selected       += OnThreadSelected;
                item.DeleteRequested += OnThreadDeleteRequested;
                Threads.Add(item);
            }
            IsThreadsEmpty = Threads.Count == 0;
        }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsLoadingThreads = false; }
    }

    // ─── Load messages ───────────────────────────────────────────────────────

    private async Task LoadMessagesAsync(Guid threadId)
    {
        IsLoadingMessages = true;
        Messages.Clear();
        IsMessagesEmpty = false;
        try
        {
            var list = await _chatService.GetMessagesAsync(threadId);
            if (list == null) return;
            foreach (var dto in list)
                Messages.Add(new MessageItemViewModel(dto));
            IsMessagesEmpty = Messages.Count == 0;
        }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsLoadingMessages = false; }
    }

    // ─── Select thread ───────────────────────────────────────────────────────

    private void OnThreadSelected(ChatThreadItemViewModel item)
    {
        foreach (var t in Threads) t.IsSelected = t.Id == item.Id;
        SelectedThread = item;
    }

    // ─── Create thread ───────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenNewThreadDialog()
    {
        NewThreadTitle       = string.Empty;
        IsNewThreadDialogOpen = true;
    }

    [RelayCommand]
    private void CloseNewThreadDialog() => IsNewThreadDialogOpen = false;

    [RelayCommand]
    private async Task CreateThreadAsync()
    {
        var title = NewThreadTitle.Trim();
        if (string.IsNullOrEmpty(title)) title = "New conversation";

        IsNewThreadDialogOpen = false;
        try
        {
            var projectId = ActiveScope == ChatScope.Project
                ? (Guid?)_projectContext.SelectedProjectId
                : null;

            var dto = await _chatService.CreateThreadAsync(
                new CreateThreadRequest(title, ActiveScope, projectId));
            if (dto == null) return;

            var item = new ChatThreadItemViewModel(dto);
            item.Selected       += OnThreadSelected;
            item.DeleteRequested += OnThreadDeleteRequested;
            Threads.Insert(0, item);
            IsThreadsEmpty = false;
            OnThreadSelected(item);
        }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
    }

    // ─── Delete thread ───────────────────────────────────────────────────────

    private async void OnThreadDeleteRequested(ChatThreadItemViewModel item)
    {
        try
        {
            var ok = await _chatService.DeleteThreadAsync(item.Id);
            if (!ok) return;
            Threads.Remove(item);
            if (SelectedThread?.Id == item.Id)
            {
                SelectedThread      = null;
                SelectedThreadTitle = "Select or create a thread";
                Messages.Clear();
                IsMessagesEmpty = true;
            }
            IsThreadsEmpty = Threads.Count == 0;
        }
        catch { /* silent */ }
    }

    // ─── Send message ─────────────────────────────────────────────────────────

    private bool CanSend() =>
        !string.IsNullOrWhiteSpace(InputText) && !IsSending && SelectedThread != null;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (SelectedThread == null) return;
        var content = InputText.Trim();
        InputText   = string.Empty;
        IsSending   = true;

        // Оптимистичное добавление сообщения пользователя
        Messages.Add(new MessageItemViewModel(new ChatMessageDto(
            Guid.NewGuid(), SelectedThread.Id,
            ChatRole.User, content, DateTime.UtcNow)));

        // Placeholder ассистента
        var pending = MessageItemViewModel.CreatePending();
        Messages.Add(pending);

        _sendCts = new CancellationTokenSource();
        try
        {
            var reply = await _chatService.SendMessageAsync(
                new SendMessageRequest(SelectedThread.Id, content), _sendCts.Token);

            Messages.Remove(pending);

            if (reply != null)
                Messages.Add(new MessageItemViewModel(reply));
            else
            {
                // Сервер вернул null (вероятно 401): убираем pending
            }
        }
        catch (OperationCanceledException)
        {
            Messages.Remove(pending);
        }
        catch (Exception ex)
        {
            Messages.Remove(pending);
            HasError     = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSending = false;
            _sendCts?.Dispose();
            _sendCts  = null;
        }
    }

    [RelayCommand]
    private void CancelSend() => _sendCts?.Cancel();

    // ─── Switch scope ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void SwitchToGlobal()  => ActiveScope = ChatScope.Global;

    [RelayCommand]
    private void SwitchToProject() => ActiveScope = ChatScope.Project;
}
