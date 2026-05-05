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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveThread))]
    private ChatThreadItemViewModel? _activeThread;

    [ObservableProperty] private string _activeThreadTitle = "Select or create a thread";
    public bool HasActiveThread => ActiveThread is not null;

    // ── Ввод ──────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _inputText = string.Empty;

    // ── Создание нового треда ─────────────────────────────────────────────────
    [ObservableProperty] private string _newThreadTitle       = string.Empty;
    [ObservableProperty] private bool   _isCreatingThread;

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
        ActiveThread       = null;
        ActiveThreadTitle  = "Select or create a thread";
        IsMessagesEmpty    = true;

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
        ActiveThread      = item;
        ActiveThreadTitle = item.Title;
        await LoadHistoryAsync(item.Id);
    }

    [RelayCommand]
    private async Task SelectThreadAsync(ChatThreadItemViewModel thread)
        => OnThreadSelected(thread);

    // ── Create thread ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void ShowCreateThread()
    {
        NewThreadTitle   = string.Empty;
        IsCreatingThread = true;
    }

    [RelayCommand]
    private void CancelCreateThread()
    {
        IsCreatingThread = false;
        NewThreadTitle   = string.Empty;
    }

    [RelayCommand]
    private async Task CreateThreadAsync()
    {
        var title = NewThreadTitle.Trim();
        if (string.IsNullOrEmpty(title)) title = "New conversation";

        IsCreatingThread = false;
        NewThreadTitle   = string.Empty;
        IsBusy           = true;
        HasError         = false;
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
            if (ActiveThread?.Id == item.Id)
            {
                ActiveThread      = null;
                ActiveThreadTitle = "Select or create a thread";
                Messages.Clear();
                IsMessagesEmpty = true;
            }
            IsThreadsEmpty = Threads.Count == 0;
        }
        catch { /* silent */ }
    }

    // ── Send (SSE streaming) ──────────────────────────────────────────────────

    private bool CanSend() =>
        !string.IsNullOrWhiteSpace(InputText) && !IsBusy && ActiveThread != null;

    /// <summary>
    /// Отправляет сообщение и принимает ответ ассистента токен за токеном через SSE.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (ActiveThread == null) return;

        var text  = InputText.Trim();
        InputText = string.Empty;
        IsBusy    = true;
        HasError  = false;

        // Оптимистичное добавление сообщения пользователя
        var userMsg = new ChatMessageViewModel(MessageRole.User, text, DateTime.UtcNow);
        Messages.Add(userMsg);

        // Placeholder ассистента (показывает индикатор генерации)
        var assistantMsg = new ChatMessageViewModel(
            MessageRole.Assistant, string.Empty, DateTime.UtcNow)
        { IsStreaming = true };
        Messages.Add(assistantMsg);

        try
        {
            var request = new SendMessageRequest(text);

            await foreach (var token in _chatService.StreamAsync(
                               ActiveThread.Id, request, _cts.Token))
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
                IsBusy = false;
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

    /// <summary>Открывает конкретный тред по Id. Используется при навигации из сайдбара.</summary>
    public async Task OpenThreadByIdAsync(Guid threadId)
    {
        var existing = Threads.FirstOrDefault(t => t.Id == threadId);
        if (existing != null) { OnThreadSelected(existing); return; }

        // Список ещё не загружен — загружаем и ищем
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
