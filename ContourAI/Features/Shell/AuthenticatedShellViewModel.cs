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
/// Начальная гидрация Sidebar выполняется в ApplyAuthAsync после загрузки данных.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

    public AuthenticatedShellViewModel(
        ConnectionSettingsStore connectionSettingsStore,
        AuthSessionStore        sessionStore,
        SessionAuthService      sessionAuthService,
        ProjectContextStore     projectContextStore,
        ChatService             chatService,
        ProjectsService         projectsService,
        DashboardViewModel      dashboardViewModel,
        ProjectsViewModel       projectsViewModel,
        ChatViewModel           chatViewModel,
        DocumentsViewModel      documentsViewModel)
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

        // Кнопка Open на карточке проекта
        Projects.ProjectOpened += OnProjectOpened;

        // Live-обновление Sidebar
        Chat.ThreadsChanged        += RebuildRecentGlobalChats;
        Projects.ProjectsChanged   += RebuildRecentProjects;

        // Резервный путь: прямые изменения коллекций
        Chat.Threads.CollectionChanged     += OnChatThreadsChanged;
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

        _currentContent = Dashboard;
    }

    public event Action? LogoutRequested;
    public event Action? SettingsRequested;

    public DashboardViewModel Dashboard { get; }
    public ProjectsViewModel  Projects  { get; }
    public ChatViewModel      Chat      { get; }
    public DocumentsViewModel Documents { get; }

    // ── Sidebar коллекции ─────────────────────────────────────────────────────

    /// <summary>Все глобальные чаты текущего пользователя (с прокруткой).</summary>
    public ObservableCollection<ChatThreadItemViewModel> RecentGlobalChats { get; } = new();

    /// <summary>Все проекты текущего пользователя (с прокруткой).</summary>
    public ObservableCollection<ProjectCardViewModel> RecentProjects { get; } = new();

    // ── Команды ───────────────────────────────────────────────────────────────

    public ICommand LogoutCommand        { get; }
    public ICommand ShowDashboardCommand { get; }
    public ICommand ShowProjectsCommand  { get; }
    public ICommand ShowChatCommand      { get; }
    public ICommand ShowDocumentsCommand { get; }
    public ICommand ShowSettingsCommand  { get; }

    public ICommand SelectGlobalChatCommand       { get; }
    public ICommand OpenProjectFromSidebarCommand { get; }

    /// <summary>
    /// Кнопка «+» рядом с «RECENT CHATS».
    /// POST /api/chat/threads/global → вставляет DTO в Chat.Threads напрямую.
    /// </summary>
    public ICommand AddNewGlobalChatCommand { get; }

    /// <summary>
    /// Кнопка «+» рядом с «RECENT PROJECTS».
    /// POST /api/projects → вставляет карточку в Projects.Projects напрямую.
    /// </summary>
    public ICommand AddNewProjectCommand { get; }

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

    /// <summary>
    /// Вызывается после успешной авторизации.
    /// Запускает начальную гидрацию Sidebar: загружает треды и проекты параллельно.
    /// </summary>
    public async Task ApplyAuthAsync(AuthTokenDto authToken, CancellationToken cancellationToken = default)
    {
        _sessionStore.Apply(authToken);
        _projectContextStore.Clear();
        CurrentContent = Dashboard;
        RaiseActiveFlags();
        await Dashboard.LoadAsync(authToken.AccessToken, cancellationToken);

        // Начальная гидрация Sidebar — параллельно, не блокируем Dashboard
        _ = HydrateSidebarAsync();
    }

    /// <summary>
    /// Загружает глобальные треды и проекты для первоначального заполнения Sidebar.
    /// Вызывается один раз после входа.
    /// </summary>
    private async Task HydrateSidebarAsync()
    {
        // Чаты: GET /api/chat/threads
        var chatsTask    = _chatService.GetGlobalThreadsAsync();
        // Проекты: GET /api/projects
        var projectsTask = _projectsService.GetProjectsAsync();

        await Task.WhenAll(chatsTask, projectsTask);

        // Заполняем Chat.Threads если ещё пусто (избегаем дублей)
        var threads = await chatsTask;
        if (threads != null && Chat.Threads.Count == 0)
        {
            foreach (var dto in threads)
            {
                var item = new ChatThreadItemViewModel(dto);
                // Подписываемся через Chat (он управляет lifecycle тредов)
                // Прямая вставка в Threads минует AddThread — используем InjectThread
                Chat.InjectThread(item);
            }
        }

        // Заполняем Projects.Projects если ещё пусто
        var projects = await projectsTask;
        if (projects != null && Projects.Projects.Count == 0)
        {
            foreach (var dto in projects)
            {
                var card = new ProjectCardViewModel(dto);
                Projects.InjectCard(card);
            }
        }

        RebuildRecentGlobalChats();
        RebuildRecentProjects();
    }

    // ── Синхронизация Sidebar ─────────────────────────────────────────────────

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

    // ── Кнопки «+» ───────────────────────────────────────────────────────────

    /// <summary>
    /// Создаёт новый глобальный тред.
    /// POST /api/chat/threads/global — название = текущая дата/время.
    /// Вставляет ChatThreadItemViewModel в начало Chat.Threads напрямую,
    /// без перезагрузки всего списка.
    /// </summary>
    private async Task AddNewGlobalChatAsync()
    {
        var title = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
        try
        {
            var dto = await _chatService.CreateGlobalAsync(
                new CreateGlobalThreadRequest(title));
            if (dto == null) return;

            var item = new ChatThreadItemViewModel(dto);
            Chat.InjectThread(item, insertAtTop: true);
            // RebuildRecentGlobalChats сработает через CollectionChanged
        }
        catch { /* silent */ }
    }

    /// <summary>
    /// Создаёт новый проект.
    /// POST /api/projects — название = текущая дата/время.
    /// Вставляет ProjectCardViewModel в начало Projects.Projects напрямую.
    /// </summary>
    private async Task AddNewProjectAsync()
    {
        var name = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
        try
        {
            var dto = await _projectsService.CreateProjectAsync(
                new CreateProjectRequest(name, string.Empty));
            if (dto == null) return;

            var summary = new ProjectSummaryDto(
                dto.Id,
                dto.Name,
                dto.Description,
                dto.AccessMode,
                dto.CreatedAtUtc,
                FolderCount: 0);

            var card = new ProjectCardViewModel(summary);
            Projects.InjectCard(card, insertAtTop: true);
            // RebuildRecentProjects сработает через CollectionChanged
        }
        catch { /* silent */ }
    }

    // ── Inline rename из Sidebar ──────────────────────────────────────────────

    /// <summary>
    /// Вызывается из ChatThreadItemViewModel.RenameRequested.
    /// Принимает готовый newTitle — EditTitle уже применён и сброшен.
    /// PUT /api/chat/threads/{id} → обновляет Title из ответа сервера.
    /// </summary>
    public async Task RenameChatFromSidebarAsync(ChatThreadItemViewModel item, string newTitle)
    {
        try
        {
            var dto = await _chatService.RenameAsync(
                item.Id, new RenameThreadRequest(newTitle));
            if (dto != null)
                item.Title = dto.Title;
        }
        catch { /* silent — Title уже обновлён оптимистично в CommitEdit */ }
    }

    /// <summary>
    /// Вызывается из ProjectCardViewModel.RenameRequested.
    /// Принимает готовый newName.
    /// PATCH /api/projects/{id}/settings — передаём name + нейтральные дефолты.
    /// Оптимистично уже обновлено в ProjectsViewModel.OnProjectRenameRequested.
    /// </summary>
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
            // Имя уже обновлено оптимистично
        }
        catch { /* silent */ }
    }

    // ── Обработчик кнопки Open ────────────────────────────────────────────────

    private void OnProjectOpened(Guid projectId)
        => _ = ShowDocumentsAsync();

    // ── Навигация ─────────────────────────────────────────────────────────────

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

    private void OnShowSettings()
        => SettingsRequested?.Invoke();

    // ── Команды Sidebar ───────────────────────────────────────────────────────

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
        await ShowDocumentsAsync();
    }

    private void RaiseActiveFlags()
    {
        RaisePropertyChanged(nameof(IsDashboardActive));
        RaisePropertyChanged(nameof(IsProjectsActive));
        RaisePropertyChanged(nameof(IsChatActive));
        RaisePropertyChanged(nameof(IsDocumentsActive));
    }
}
