/// <summary>
/// ViewModel рабочего пространства проекта.
/// Открывается по клику на проект из SidebarView.
/// Вкладки: Settings, Folder, Documents, Chat.
///
/// Chat-вкладка (lazy):
///   При первом переключении на WorkspaceTab.Chat вызывается InitializeChatAsync:
///   - GET /api/chat/projects/{id}/threads
///   - Нет тредов → HasProjectChat = false (показывается кнопка "Начать чат")
///   - Есть треды  → берётся первый, создаётся ChatViewModel, загружается история
///   - StartChatCommand → POST /api/chat/threads → InitializeChatAsync повторно
///
/// При создании нового проекта (CreateProjectAsync) чат создаётся автоматически.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Chat;
using ContourAI.Entities.Projects;
using ContourAI.Features.Chat;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Features.Projects;

/// <summary>Индекс активной вкладки workspace.</summary>
public enum WorkspaceTab
{
    Settings  = 0,
    Folder    = 1,
    Documents = 2,
    Chat      = 3
}

public sealed partial class ProjectWorkspaceViewModel : ObservableObject
{
    private readonly ProjectsService  _projectsService;
    private readonly ChatService      _chatService;
    private readonly ProjectContextStore _projectContextStore;
    private CancellationTokenSource   _cts = new();
    private bool                      _documentsLoaded;
    private bool                      _chatInitialized;

    // ─── Идентификация ──────────────────────────────────────────────────────────

    public Guid ProjectId { get; private set; }

    // ─── Заголовок ─────────────────────────────────────────────────────────────

    [ObservableProperty] private string _projectName = string.Empty;

    // ─── Вкладки ───────────────────────────────────────────────────────────────

    [ObservableProperty] private int _selectedTabIndex = (int)WorkspaceTab.Settings;

    // ─── Состояния загрузки ────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    // ─── Вложенные ViewModel ───────────────────────────────────────────────────

    [ObservableProperty] private ProjectSettingsDialogViewModel? _settingsViewModel;

    /// <summary>ViewModel вкладки Documents.</summary>
    public ProjectDocumentsViewModel DocumentsViewModel { get; }

    // ─── Chat ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// ViewModel чата проекта. Null пока чат не инициализирован
    /// или пока нет ни одного треда.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowChatView))]
    [NotifyPropertyChangedFor(nameof(ShowStartChatButton))]
    private ChatViewModel? _projectChatViewModel;

    /// <summary>true — идёт загрузка тредов на вкладке Chat.</summary>
    [ObservableProperty] private bool _isChatLoading;

    /// <summary>true — тред найден, ChatView отображается.</summary>
    public bool ShowChatView        => ProjectChatViewModel is not null;

    /// <summary>true — тредов нет, отображается кнопка "Начать чат".</summary>
    public bool ShowStartChatButton => ProjectChatViewModel is null && !IsChatLoading;

    // ─── События ───────────────────────────────────────────────────────────────

    public event Action? BackRequested;
    public event Action? ProjectDeleted;

    public ProjectWorkspaceViewModel(
        ProjectsService           projectsService,
        ChatService               chatService,
        ProjectContextStore       projectContextStore,
        ProjectDocumentsViewModel documentsViewModel)
    {
        _projectsService     = projectsService;
        _chatService         = chatService;
        _projectContextStore = projectContextStore;
        DocumentsViewModel   = documentsViewModel;
    }

    // ─── Инициализация ─────────────────────────────────────────────────────────

    /// <summary>Вызывается из Shell при клике на проект в Sidebar.</summary>
    public async Task OpenAsync(Guid projectId, string projectName, int folderCount = 0)
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        _documentsLoaded = false;
        _chatInitialized = false;

        ProjectId        = projectId;
        ProjectName      = projectName;
        SelectedTabIndex = (int)WorkspaceTab.Settings;
        HasError         = false;
        ErrorMessage     = string.Empty;

        // Сбрасываем старый ChatViewModel при смене проекта
        ProjectChatViewModel = null;

        var settingsVm            = new ProjectSettingsDialogViewModel(projectId, _projectsService);
        settingsVm.Closed        += () => BackRequested?.Invoke();
        settingsVm.HasFolderAttached = folderCount > 0;
        settingsVm.Deleted += () => ProjectDeleted?.Invoke();
        SettingsViewModel = settingsVm;

        await LoadSettingsAsync(_cts.Token);
    }

    // ─── Загрузка настроек ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadSettingsAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        HasError  = false;
        try
        {
            var settings = await _projectsService.GetProjectSettingsAsync(ProjectId, ct);
            if (settings != null && SettingsViewModel != null)
                ApplySettings(settings, SettingsViewModel);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    private static void ApplySettings(ProjectSettingsDto dto, ProjectSettingsDialogViewModel vm)
    {
        vm.SystemPrompt      = dto.SystemPrompt;
        vm.MaxTokens         = dto.MaxTokens;
        vm.Temperature       = dto.Temperature;
        vm.RagTopK           = dto.RagTopK;
        vm.UseRagContext     = dto.UseRagContext;
        vm.ContextWindowSize = dto.ContextWindowSize;
    }

    // ─── Навигация по вкладкам ─────────────────────────────────────────────────

    [RelayCommand]
    private void SelectTab(WorkspaceTab tab)
    {
        SelectedTabIndex = (int)tab;

        if (tab == WorkspaceTab.Documents && !_documentsLoaded)
        {
            _documentsLoaded = true;
            _ = DocumentsViewModel.LoadAsync(ProjectId, _cts.Token);
        }

        // Lazy-инициализация Chat — один раз за жизнь проекта.
        // При повторном переключении на Chat ничего лишнего не делаем.
        if (tab == WorkspaceTab.Chat && !_chatInitialized)
        {
            _chatInitialized = true;
            _ = InitializeChatAsync(_cts.Token);
        }
    }

    // ─── Chat ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Загружает треды проекта.
    /// Если треды есть — берёт первый, создаёт ChatViewModel, загружает историю.
    /// Если тредов нет — ProjectChatViewModel остаётся null (кнопка "Начать чат").
    /// </summary>
    private async Task InitializeChatAsync(CancellationToken ct)
    {
        IsChatLoading        = true;
        ProjectChatViewModel = null;
        try
        {
            var threads = await _chatService.GetThreadsByProjectAsync(ProjectId, ct);
            if (threads == null || threads.Count == 0)
                return; // кнопка "Начать чат"

            // Берём первый попавшийся тред
            var firstThread = threads.First();
            var vm = BuildChatViewModel();
            await vm.InitializeAsync();

            // Сразу открываем нужный тред без ожидания пользовательского клика
            await vm.OpenThreadByIdAsync(firstThread.Id);

            ProjectChatViewModel = vm;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsChatLoading = false; }
    }

    /// <summary>
    /// Команда кнопки "Начать чат".
    /// POST /api/chat/threads → инициализирует ChatViewModel с новым тредом.
    /// </summary>
    [RelayCommand]
    private async Task StartChatAsync()
    {
        IsChatLoading = true;
        HasError      = false;
        try
        {
            var title = $"Chat {DateTime.Now:dd.MM.yyyy HH:mm}";
            var dto   = await _chatService.CreateInProjectAsync(
                new CreateThreadRequest(ProjectId, title), _cts.Token);
            if (dto == null) return;

            var vm = BuildChatViewModel();
            await vm.InitializeAsync();
            await vm.OpenThreadByIdAsync(dto.Id);

            ProjectChatViewModel = vm;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsChatLoading = false; }
    }

    /// <summary>
    /// Создаёт ChatViewModel в режиме проектного чата.
    /// showBackButton=false — кнопка «Назад» не нужна (вкладка workspace),
    /// headerTitle=null — берётся дефолтный "Project Chat".
    /// </summary>
    private ChatViewModel BuildChatViewModel()
        => new(_chatService, _projectContextStore,
               projectId:      ProjectId,
               headerTitle:    ProjectName,
               showBackButton: false);

    // ─── Кнопка «Назад» ────────────────────────────────────────────────────────

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();
}