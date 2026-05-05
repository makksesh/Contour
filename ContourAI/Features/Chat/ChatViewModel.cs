/// <summary>
/// ViewModel экрана чата.
/// Поддерживает глобальный и проектный режим.
/// Отправка сообщений — SSE-стриминг через ChatService.StreamAsync.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
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

    // ── Событие «назад» ──────────────────────────────────────────────────────
    public event Action? OnBack;

    // ── Список тредов (левая панель) ─────────────────────────────────────────
    public ObservableCollection<ChatThreadItemViewModel> Threads  { get; } = new();

    // ── Сообщения активного треда ─────────────────────────────────────────────
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();

    // ── Активный тред ────────────────────────────────────────────────────────
    /// <summary>Активный тред (AXAML: SelectedThread).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveThread))]
    [NotifyPropertyChangedFor(nameof(SelectedThreadTitle))]
    private ChatThreadItemViewModel? _selectedThread;

    /// <summary>Заголовок активного треда для хедера (AXAML: SelectedThreadTitle).</summary>
    public string SelectedThreadTitle
        => _selectedThread?.Title ?? "Select or create a thread";

    public bool HasActiveThread => SelectedThread is not null;

    // ── Ввод ──────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _inputText = string.Empty;

    // ── Scope switcher ────────────────────────────────────────────────────────
    /// <summary>Метка текущего scope (GLOBAL / PROJECT) для хедера чата.</summary>
    [ObservableProperty] private string _scopeLabel = "GLOBAL";

    // ── Создание нового треда (диалог) ────────────────────────────────────────
    [ObservableProperty] private string _newThreadTitle        = string.Empty;
    [ObservableProperty] private bool   _isNewThreadDialogOpen;

    // ── Флаг отправки (для кнопки «Отмена» и блокировки Send) ────────────────
    /// <summary>true пока идёт SSE-стриминг ответа ассистента.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isSending;

    // ── Состояния загрузки / ошибок ───────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isBusy;

    [ObservableProperty] private bool   _isLoadingThreads;
    [ObservableProperty] private bool   _isLoadingMessages;
    [ObservableProperty] private bool   _isThreadsEmpty;
    [ObservableProperty] private bool   _isMessagesEmpty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool   _hasError;

    // ── Заголовки / видимость ─────────────────────────────────────────────────
    [ObservableProperty] private string _headerTitle     = "AI Assistant";
    [ObservableProperty] private string _threadListTitle = "Threads";
    [ObservableProperty] private bool   _showBackButton  = true;

    private readonly Guid? _projectId;

    public ChatViewModel(
        ChatService         chatService,
        ProjectContextStore projectContext,
        Guid?               projectId      = null,
        string?             headerTitle    = null,
        bool                showBackButton = true)
    {
        _chatService    = chatService;
        _projectContext = projectContext;
        _projectId      = projectId;

        HeaderTitle     = headerTitle ?? (projectId is null ? "AI Assistant" : "Project Chat");
        ThreadListTitle = projectId is null ? "Threads" : "Project Threads";
        ShowBackButton  = showBackButton;
        ScopeLabel      = projectId is null ? "GLOBAL" : "PROJECT";
    }

    // ── Initialize ────────────────────────────────────────────────────────────

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
            List<ChatThreadDto>? list;
            if (_projectId.HasValue)
                list = await _chatService.GetThreadsByProjectAsync(_projectId.Value, _cts.Token);
            else
                list = await _chatService.GetGlobalThreadsAsync(_cts.Token);

            if (list == null) return;

            foreach (var dto in list)
            {
                var item = new ChatThreadItemViewModel(dto);
                item.Selected        += OnThreadSelected;
                item.DeleteRequested += OnThreadDeleteRequested;
                Threads.Add(item);
            }
            IsThreadsEmpty = Threads.Count == 0;
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

    // ── Scope switch commands (AXAML: SwitchToGlobalCommand / SwitchToProjectCommand) ──

    /// <summary>Переключает вид на глобальные треды.</summary>
    [RelayCommand]
    private async Task SwitchToGlobalAsync()
    {
        ScopeLabel = "GLOBAL";
        await LoadThreadsAsync();
    }

    /// <summary>Переключает вид на треды проекта.</summary>
    [RelayCommand]
    private async Task SwitchToProjectAsync()
    {
        ScopeLabel = "PROJECT";
        await LoadThreadsAsync();
    }

    // ── New thread dialog commands ────────────────────────────────────────────

    /// <summary>Открывает диалог создания треда (AXAML: OpenNewThreadDialogCommand).</summary>
    [RelayCommand]
    private void OpenNewThreadDialog()
    {
        NewThreadTitle        = string.Empty;
        IsNewThreadDialogOpen = true;
    }

    /// <summary>Закрывает диалог без создания (AXAML: CloseNewThreadDialogCommand).</summary>
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
        if (string.IsNullOrEmpty(title)) title = "New conversation";

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
            item.Selected        += OnThreadSelected;
            item.DeleteRequested += OnThreadDeleteRequested;
            Threads.Insert(0, item);
            IsThreadsEmpty = false;
            OnThreadSelected(item);
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
        }
        catch { /* silent */ }
    }

    // ── Send (SSE streaming) ──────────────────────────────────────────────────

    private bool CanSend() =>
        !string.IsNullOrWhiteSpace(InputText) && !IsBusy && !IsSending && SelectedThread != null;

    /// <summary>
    /// Отправляет сообщение и принимает ответ ассистента токен за токеном через SSE.
    /// </summary>
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
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(
                () => { ErrorMessage = $"Send error: {ex.Message}"; HasError = true; });
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

    // ── Open thread by Id (sidebar navigation) ────────────────────────────────

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
}
