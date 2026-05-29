using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Chat;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Features.Chat;

public sealed partial class ChatViewModel : ObservableObject
{
    private readonly ChatService         _chatService;
    private readonly ProjectContextStore _projectContext;
    private CancellationTokenSource      _cts = new();

    public event Action? OnBack;

    /// <summary>
    /// Поднимается после создания треда или отправки сообщения.
    /// AuthenticatedShellViewModel подписывается и перестраивает RecentGlobalChats.
    /// </summary>
    public event Action? ThreadsChanged;

    public ObservableCollection<ChatThreadItemViewModel> Threads  { get; } = new();
    public ObservableCollection<ChatMessageViewModel>    Messages { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveThread))]
    [NotifyPropertyChangedFor(nameof(SelectedThreadTitle))]
    private ChatThreadItemViewModel? _selectedThread;

    public string SelectedThreadTitle
        => _selectedThread?.Title ?? "Выберите чат или создайте новый";

    public bool HasActiveThread => SelectedThread is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty] private string _scopeLabel = "ГЛОБАЛЬНЫЙ";
    [ObservableProperty] private string _newThreadTitle        = string.Empty;
    [ObservableProperty] private bool   _isNewThreadDialogOpen;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isSending;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isBusy;

    [ObservableProperty] private bool   _isLoadingThreads;
    [ObservableProperty] private bool   _isLoadingMessages;
    [ObservableProperty] private bool   _isThreadsEmpty;
    [ObservableProperty] private bool   _isMessagesEmpty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _headerTitle     = "AI-ассистент";
    [ObservableProperty] private string _threadListTitle = "Чаты";
    [ObservableProperty] private bool   _showBackButton  = true;
    [ObservableProperty] private bool   _isRagEnabled;
    [ObservableProperty] private int    _ragTopK = 5;

    private readonly Guid? _projectId;

    public bool RagBadgeVisible => _projectId.HasValue && IsRagEnabled;
    public string RagBadgeText  => $"RAG активен · {RagTopK} чанков";

    public ChatViewModel(
        ChatService         chatService,
        ProjectContextStore projectContext,
        Guid?               projectId      = null,
        string?             headerTitle    = null,
        bool                showBackButton = true,
        bool                isRagEnabled   = false,
        int                 ragTopK        = 5)
    {
        _chatService    = chatService;
        _projectContext = projectContext;
        _projectId      = projectId;

        HeaderTitle     = headerTitle ?? (projectId is null ? "AI-ассистент" : "Чат проекта");
        ThreadListTitle = projectId is null ? "Чаты" : "Чаты проекта";
        ShowBackButton  = showBackButton;
        ScopeLabel      = projectId is null ? "ГЛОБАЛЬНЫЙ" : "ПРОЕКТ";
        IsRagEnabled    = isRagEnabled;
        RagTopK         = ragTopK;
    }

    public async Task InitializeAsync()
    {
        await LoadThreadsAsync();
    }

    // ── Load threads ──────────────────────────────────────────────────────────

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
            System.Collections.Generic.List<ChatThreadDto>? list;
            if (_projectId.HasValue)
                list = await _chatService.GetThreadsByProjectAsync(_projectId.Value, _cts.Token);
            else
                list = await _chatService.GetGlobalThreadsAsync(_cts.Token);

            if (list == null) return;

            foreach (var dto in list)
                AddThread(new ChatThreadItemViewModel(dto));

            IsThreadsEmpty = Threads.Count == 0;
            ThreadsChanged?.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsLoadingThreads = false; }
    }

    // ── Load history ──────────────────────────────────────────────────────────

    private async Task LoadHistoryAsync(Guid threadId)
    {
        IsLoadingMessages = true;
        Messages.Clear();
        IsMessagesEmpty = false;
        try
        {
            var result = await _chatService.GetHistoryAsync(threadId, _cts.Token);
            if (result == null) return;
            foreach (var msg in result.Messages)
                Messages.Add(ChatMessageViewModel.FromDto(msg));
            IsMessagesEmpty = Messages.Count == 0;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsLoadingMessages = false; }
    }

    // ── Select thread ─────────────────────────────────────────────────────────

    private async void OnThreadSelected(ChatThreadItemViewModel item)
    {
        foreach (var t in Threads) t.IsSelected = t.Id == item.Id;
        SelectedThread = item;
        await LoadHistoryAsync(item.Id);
    }

    [RelayCommand]
    private async Task SelectThreadAsync(ChatThreadItemViewModel thread)
        => OnThreadSelected(thread);

    // ── Scope switch ──────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SwitchToGlobalAsync()
    {
        ScopeLabel = "ГЛОБАЛЬНЫЙ";
        await LoadThreadsAsync();
    }

    [RelayCommand]
    private async Task SwitchToProjectAsync()
    {
        ScopeLabel = "ПРОЕКТ";
        await LoadThreadsAsync();
    }

    // ── New thread dialog ─────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenNewThreadDialog()
    {
        NewThreadTitle        = string.Empty;
        IsNewThreadDialogOpen = true;
    }

    [RelayCommand]
    private void CloseNewThreadDialog()
    {
        IsNewThreadDialogOpen = false;
        NewThreadTitle        = string.Empty;
    }

    // ── Create thread ─────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task CreateThreadAsync()
    {
        var title = NewThreadTitle.Trim();
        if (string.IsNullOrEmpty(title))
            title = $"{DateTime.Now:dd.MM.yyyy HH:mm:ss}";

        IsNewThreadDialogOpen = false;
        NewThreadTitle        = string.Empty;
        IsBusy                = true;
        HasError              = false;
        try
        {
            ChatThreadDto? dto;
            if (_projectId.HasValue)
                dto = await _chatService.CreateInProjectAsync(
                    new CreateThreadRequest(_projectId.Value, title), _cts.Token);
            else
                dto = await _chatService.CreateGlobalAsync(
                    new CreateGlobalThreadRequest(title), _cts.Token);

            if (dto == null) return;

            var item = new ChatThreadItemViewModel(dto);
            AddThread(item, insertAtTop: true);
            IsThreadsEmpty = false;
            OnThreadSelected(item);
            ThreadsChanged?.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    // ── Delete thread ─────────────────────────────────────────────────────────

    private async void OnThreadDeleteRequested(ChatThreadItemViewModel item)
    {
        try
        {
            await _chatService.DeleteThreadAsync(item.Id, _cts.Token);
            Threads.Remove(item);
            if (SelectedThread?.Id == item.Id)
            {
                SelectedThread  = null;
                Messages.Clear();
                IsMessagesEmpty = true;
            }
            IsThreadsEmpty = Threads.Count == 0;
            ThreadsChanged?.Invoke();
        }
        catch { /* silent */ }
    }

    // ── Rename thread ─────────────────────────────────────────────────────────

    /// <summary>
    /// Вызывается из ChatThreadItemViewModel.RenameRequested.
    /// newTitle — уже готовое значение после CommitEdit.
    /// PUT /api/chat/threads/{id} → обновляем Title из ответа.
    /// </summary>
    private async void OnThreadRenameRequested(ChatThreadItemViewModel item, string newTitle)
    {
        try
        {
            var dto = await _chatService.RenameAsync(
                item.Id, new RenameThreadRequest(newTitle), _cts.Token);
            if (dto != null)
            {
                item.Title = dto.Title;
                ThreadsChanged?.Invoke();
            }
        }
        catch { /* silent */ }
    }

    // ── Send (SSE streaming) ──────────────────────────────────────────────────

    private bool CanSend() =>
        !string.IsNullOrWhiteSpace(InputText) && !IsBusy && !IsSending && SelectedThread != null;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (SelectedThread == null) return;

        var text  = InputText.Trim();
        InputText = string.Empty;
        IsBusy    = true;
        IsSending = true;
        HasError  = false;

        var userMsg = new ChatMessageViewModel(MessageRole.User, text, DateTime.UtcNow);
        Messages.Add(userMsg);

        var assistantMsg = new ChatMessageViewModel(
            MessageRole.Assistant, string.Empty, DateTime.UtcNow)
        { IsStreaming = true };
        Messages.Add(assistantMsg);

        try
        {
            var request = new SendMessageRequest(text);

            await foreach (var token in _chatService.StreamAsync(
                               SelectedThread.Id, request, _cts.Token))
            {
                await Dispatcher.UIThread.InvokeAsync(
                    () => assistantMsg.AppendToken(token));
            }

            ThreadsChanged?.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(
                    () => { ErrorMessage = $"Ошибка отправки: {ex.Message}"; HasError = true; });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                assistantMsg.IsStreaming = false;
                IsBusy    = false;
                IsSending = false;
            });
        }
    }

    [RelayCommand]
    private void CancelSend()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    // ── Open thread by Id ─────────────────────────────────────────────────────

    public async Task OpenThreadByIdAsync(Guid threadId)
    {
        var existing = Threads.FirstOrDefault(t => t.Id == threadId);
        if (existing != null) { OnThreadSelected(existing); return; }

        await LoadThreadsAsync();
        var found = Threads.FirstOrDefault(t => t.Id == threadId);
        if (found != null) OnThreadSelected(found);
    }

    // ── GoBack ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void GoBack()
    {
        _cts.Cancel();
        OnBack?.Invoke();
    }

    // ── Public injection API (для Shell / Sidebar) ────────────────────────────

    /// <summary>
    /// Вставляет тред в коллекцию Threads с подпиской на события.
    /// Используется Shell при начальной гидрации Sidebar и при создании
    /// нового треда из кнопки «+» без перезагрузки всего списка.
    /// </summary>
    public void InjectThread(ChatThreadItemViewModel item, bool insertAtTop = false)
        => AddThread(item, insertAtTop);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AddThread(ChatThreadItemViewModel item, bool insertAtTop = false)
    {
        item.Selected        += OnThreadSelected;
        item.DeleteRequested += OnThreadDeleteRequested;
        item.RenameRequested += OnThreadRenameRequested;
        if (insertAtTop)
            Threads.Insert(0, item);
        else
            Threads.Add(item);
    }
}
