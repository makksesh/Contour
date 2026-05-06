/// <summary>
/// ViewModel авторизованного shell-экрана.
/// Управляет навигацией: Dashboard, Projects, Chat, Documents.
/// Предоставляет RecentGlobalChats и RecentProjects для SidebarView.
///
/// Live-обновление Sidebar:
///   - Chat.ThreadsChanged    → RebuildRecentGlobalChats()
///   - Projects.ProjectsChanged → RebuildRecentProjects()
///   - CollectionChanged на обеих коллекциях — резервный путь.
///
/// Кнопки «+» в Sidebar:
///   AddNewGlobalChatCommand  — POST /api/chat/threads/global, вставка в коллекцию напрямую.
///   AddNewProjectCommand     — POST /api/projects,           вставка в коллекцию напрямую.
///
/// Inline rename из Sidebar:
///   RenameChatFromSidebar    — получает (item, newTitle) через событие, PUT /api/chat/threads/{id}.
///   RenameProjectFromSidebar — получает (card, newName)  через событие, PATCH /api/projects/{id}/settings.
///
/// Начальная гидратация Sidebar выполняется в ApplyAuthAsync после загрузки данных.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Chat;
using ContourAI.Entities.Projects;
using ContourAI.Features.Auth;
using ContourAI.Features.Chat;
using ContourAI.Features.Dashboard;
using ContourAI.Features.Documents;
using ContourAI.Features.Projects;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Features.Shell;

public sealed class AuthenticatedShellViewModel : ViewModelBase
{
    private readonly ConnectionSettingsStore _connectionSettingsStore;
    private readonly AuthSessionStore        _sessionStore;
    private readonly SessionAuthService      _sessionAuthService;
    private readonly ProjectContextStore     _projectContextStore;
    private readonly ChatService             _chatService;
    private readonly ProjectsService         _projectsService;
    private object?                          _currentContent;
    private object?                          _previousContent;

    public AuthenticatedShellViewModel(
        ConnectionSettingsStore   connectionSettingsStore,
        AuthSessionStore          sessionStore,
        SessionAuthService        sessionAuthService,
        ProjectContextStore       projectContextStore,
        ChatService               chatService,
        ProjectsService           projectsService,
        DashboardViewModel        dashboardViewModel,
        ProjectsViewModel         projectsViewModel,
        ChatViewModel             chatViewModel,
        DocumentsViewModel        documentsViewModel,
        ProjectWorkspaceViewModel projectWorkspaceViewModel)
    {
        _connectionSettingsStore = connectionSettingsStore;
        _sessionStore            = sessionStore;
        _sessionAuthService      = sessionAuthService;
        _projectContextStore     = projectContextStore;
        _chatService             = chatService;
        _projectsService         = projectsService;

        Dashboard = dashboardViewModel;
        Projects  = projectsViewModel;
        Chat      = chatViewModel;
        Documents = documentsViewModel;
        Workspace = projectWorkspaceViewModel;

        LogoutCommand        = new RelayCommand(() => _ = LogoutAsync());
        ShowDashboardCommand = new RelayCommand(() => ShowSection(Dashboard));
        ShowProjectsCommand  = new AsyncRelayCommand(ShowProjectsAsync);
        ShowChatCommand      = new AsyncRelayCommand(ShowChatAsync);
        ShowDocumentsCommand = new AsyncRelayCommand(ShowDocumentsAsync);
        ShowSettingsCommand  = new RelayCommand(OnShowSettings);

        SelectGlobalChatCommand       = new AsyncRelayCommand<ChatThreadItemViewModel>(SelectGlobalChatAsync);
        OpenProjectFromSidebarCommand = new AsyncRelayCommand<ProjectCardViewModel>(OpenProjectFromSidebarAsync);

        AddNewGlobalChatCommand = new AsyncRelayCommand(AddNewGlobalChatAsync);
        AddNewProjectCommand    = new AsyncRelayCommand(AddNewProjectAsync);

        Projects.ProjectOpened += OnProjectOpened;

        Chat.ThreadsChanged         += RebuildRecentGlobalChats;
        Projects.ProjectsChanged    += RebuildRecentProjects;

        Chat.Threads.CollectionChanged      += OnChatThreadsChanged;
        Projects.Projects.CollectionChanged += OnProjectsCollectionChanged;

        _connectionSettingsStore.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ConnectionSettingsStore.ServerIp)
                or nameof(ConnectionSettingsStore.ServerIpDisplay))
                RaisePropertyChanged(nameof(ServerIpDisplay));
        };

        _sessionStore.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AuthSessionStore.CurrentUsername))
                RaisePropertyChanged(nameof(Username));
        };

        Workspace.BackRequested += OnWorkspaceBackRequested;

        /// <summary>
        /// Подписываемся на ProjectDeleted — событие выбрасывается из ProjectWorkspaceViewModel
        /// после удаления проекта. Не трогаем null SettingsViewModel напрямую.
        /// </summary>
        Workspace.ProjectDeleted += OnProjectDeleted;

        _currentContent = Dashboard;
    }

    public event Action? LogoutRequested;
    public event Action? SettingsRequested;

    public DashboardViewModel     Dashboard { get; }
    public ProjectsViewModel      Projects  { get; }
    public ChatViewModel          Chat      { get; }
    public DocumentsViewModel     Documents { get; }
    public ProjectWorkspaceViewModel Workspace { get; }

    // ─── Sidebar коллекции ─────────────────────────────────────────────────────

    public ObservableCollection<ChatThreadItemViewModel> RecentGlobalChats { get; } = new();
    public ObservableCollection<ProjectCardViewModel>    RecentProjects     { get; } = new();

    // ─── Команды ──────────────────────────────────────────────────────────────

    public ICommand LogoutCommand        { get; }
    public ICommand ShowDashboardCommand { get; }
    public ICommand ShowProjectsCommand  { get; }
    public ICommand ShowChatCommand      { get; }
    public ICommand ShowDocumentsCommand { get; }
    public ICommand ShowSettingsCommand  { get; }
    public ICommand SelectGlobalChatCommand       { get; }
    public ICommand OpenProjectFromSidebarCommand { get; }
    public ICommand AddNewGlobalChatCommand       { get; }
    public ICommand AddNewProjectCommand          { get; }

    public string Username        => _sessionStore.CurrentUsername;
    public string ServerIpDisplay => _connectionSettingsStore.ServerIpDisplay;

    public object? CurrentContent
    {
        get => _currentContent;
        private set => SetProperty(ref _currentContent, value);
    }

    public bool IsDashboardActive => CurrentContent is DashboardViewModel;
    public bool IsProjectsActive  => CurrentContent is ProjectsViewModel;
    public bool IsChatActive      => CurrentContent is ChatViewModel;
    public bool IsDocumentsActive => CurrentContent is DocumentsViewModel;

    // ─── Auth ─────────────────────────────────────────────────────────────────────

    public async Task ApplyAuthAsync(AuthTokenDto authToken, CancellationToken cancellationToken = default)
    {
        _sessionStore.Apply(authToken);
        _projectContextStore.Clear();
        CurrentContent = Dashboard;
        RaiseActiveFlags();
        await Dashboard.LoadAsync(authToken.AccessToken, cancellationToken);
        _ = HydrateSidebarAsync();
    }

    private async Task HydrateSidebarAsync()
    {
        var chatsTask    = _chatService.GetGlobalThreadsAsync();
        var projectsTask = _projectsService.GetProjectsAsync();
        await Task.WhenAll(chatsTask, projectsTask);

        var threads = await chatsTask;
        if (threads != null && Chat.Threads.Count == 0)
            foreach (var dto in threads)
                Chat.InjectThread(new ChatThreadItemViewModel(dto));

        var projects = await projectsTask;
        if (projects != null && Projects.Projects.Count == 0)
            foreach (var dto in projects)
                Projects.InjectCard(new ProjectCardViewModel(dto));

        RebuildRecentGlobalChats();
        RebuildRecentProjects();
    }

    // ─── Sidebar ───────────────────────────────────────────────────────────────────

    private void RebuildRecentGlobalChats()
    {
        RecentGlobalChats.Clear();
        foreach (var t in Chat.Threads)
            if (t.IsGlobal)
                RecentGlobalChats.Add(t);
    }

    private void RebuildRecentProjects()
    {
        RecentProjects.Clear();
        foreach (var p in Projects.Projects)
            RecentProjects.Add(p);
    }

    private void OnChatThreadsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RebuildRecentGlobalChats();

    private void OnProjectsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RebuildRecentProjects();

    // ─── Кнопки «+» ────────────────────────────────────────────────────────────────

    private async Task AddNewGlobalChatAsync()
    {
        var title = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
        try
        {
            var dto = await _chatService.CreateGlobalAsync(new CreateGlobalThreadRequest(title));
            if (dto == null) return;
            Chat.InjectThread(new ChatThreadItemViewModel(dto), insertAtTop: true);
        }
        catch { /* silent */ }
    }

    private async Task AddNewProjectAsync()
    {
        var name = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
        try
        {
            var dto = await _projectsService.CreateProjectAsync(
                new CreateProjectRequest(name, string.Empty));
            if (dto == null) return;

            var summary = new ProjectSummaryDto(
                dto.Id, dto.Name, dto.Description,
                dto.AccessMode, dto.CreatedAtUtc, FolderCount: 0);
            Projects.InjectCard(new ProjectCardViewModel(summary), insertAtTop: true);

            // Автоматически создаём первый чат-тред для нового проекта
            _ = _chatService.CreateInProjectAsync(
                new CreateThreadRequest(dto.Id, $"Chat {DateTime.Now:dd.MM.yyyy}"));
        }
        catch { /* silent */ }
    }

    // ─── Inline rename ──────────────────────────────────────────────────────────

    public async Task RenameChatFromSidebarAsync(ChatThreadItemViewModel item, string newTitle)
    {
        try
        {
            var dto = await _chatService.RenameAsync(item.Id, new RenameThreadRequest(newTitle));
            if (dto != null) item.Title = dto.Title;
        }
        catch { }
    }

    public async Task RenameProjectFromSidebarAsync(ProjectCardViewModel card, string newName)
    {
        try
        {
            await _projectsService.UpdateSettingsAsync(
                card.Id,
                new UpdateProjectSettingsRequest(
                    ChatModelEndpointId:      null,
                    EmbeddingModelEndpointId: null,
                    SystemPrompt:             string.Empty,
                    MaxTokens:                4096,
                    Temperature:              0.7f,
                    RagTopK:                  5,
                    UseRagContext:            false,
                    ContextWindowSize:        10));
        }
        catch { }
    }

    // ─── Обработчик Open ─────────────────────────────────────────────────────────

    private void OnProjectOpened(Guid projectId)
    {
        var card = Projects.Projects.FirstOrDefault(c => c.Id == projectId);
        if (card == null) return;
        _ = OpenProjectFromSidebarAsync(card);
    }

    // ─── Навигация ──────────────────────────────────────────────────────────────────

    private void ShowSection(object section)
    {
        CurrentContent = section;
        RaiseActiveFlags();
    }

    private async Task ShowProjectsAsync()
    {
        CurrentContent = Projects;
        RaiseActiveFlags();
        await Projects.InitializeAsync();
    }

    private async Task ShowChatAsync()
    {
        CurrentContent = Chat;
        RaiseActiveFlags();
        await Chat.InitializeAsync();
        RebuildRecentGlobalChats();
    }

    private async Task ShowDocumentsAsync()
    {
        CurrentContent = Documents;
        RaiseActiveFlags();
        await Documents.InitializeAsync();
    }

    private async Task LogoutAsync()
    {
        await _sessionAuthService.LogoutAsync();
        _projectContextStore.Clear();
        Dashboard.Clear();
        RecentGlobalChats.Clear();
        RecentProjects.Clear();
        LogoutRequested?.Invoke();
    }

    private void OnShowSettings() => SettingsRequested?.Invoke();

    private async Task SelectGlobalChatAsync(ChatThreadItemViewModel? thread)
    {
        if (thread == null) return;
        CurrentContent = Chat;
        RaiseActiveFlags();
        if (Chat.Threads.Count == 0)
            await Chat.InitializeAsync();
        await Chat.OpenThreadByIdAsync(thread.Id);
    }

    private async Task OpenProjectFromSidebarAsync(ProjectCardViewModel? card)
    {
        if (card == null) return;
        _projectContextStore.Select(card.Id, card.Name);
        _previousContent = CurrentContent;
        CurrentContent   = Workspace;
        RaiseActiveFlags();
        await Workspace.OpenAsync(card.Id, card.Name, card.FolderCount);
    }

    private void OnWorkspaceBackRequested()
    {
        CurrentContent   = _previousContent ?? Dashboard;
        _previousContent = null;
        RaiseActiveFlags();
    }

    /// <summary>
    /// Вызывается после удаления проекта из вкладки Settings.
    /// Возвращает на предыдущий экран + обновляет Sidebar.
    /// </summary>
    private void OnProjectDeleted()
    {
        CurrentContent   = _previousContent ?? Dashboard;
        _previousContent = null;
        RaiseActiveFlags();
        _ = Projects.LoadProjectsAsync();
    }

    private void RaiseActiveFlags()
    {
        RaisePropertyChanged(nameof(IsDashboardActive));
        RaisePropertyChanged(nameof(IsProjectsActive));
        RaisePropertyChanged(nameof(IsChatActive));
        RaisePropertyChanged(nameof(IsDocumentsActive));
    }
}
