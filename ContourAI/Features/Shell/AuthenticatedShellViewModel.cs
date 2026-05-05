/// <summary>
/// ViewModel авторизованного shell-экрана.
/// Управляет навигацией: Projects, Chat, Documents.
/// Предоставляет RecentGlobalChats и RecentProjects для SidebarView.
/// Подписывается на Chat.ThreadsChanged и Projects.ProjectsChanged
/// для live-обновления Sidebar без ручного refresh.
/// AddNewGlobalChatCommand / AddNewProjectCommand — кнопки «+» в Sidebar.
/// Inline rename чатов и проектов через ChatService / ProjectsService.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
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
    private object? _currentContent;

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

        // Кнопки «+» в Sidebar
        AddNewGlobalChatCommand = new AsyncRelayCommand(AddNewGlobalChatAsync);
        AddNewProjectCommand    = new AsyncRelayCommand(AddNewProjectAsync);

        // Переименование из Sidebar
        RenameChatFromSidebarCommand    = new AsyncRelayCommand<ChatThreadItemViewModel>(RenameChatFromSidebarAsync);
        RenameProjectFromSidebarCommand = new AsyncRelayCommand<ProjectCardViewModel>(RenameProjectFromSidebarAsync);

        // Кнопка Open на карточке проекта
        Projects.ProjectOpened += OnProjectOpened;

        // Live-обновление Sidebar при изменениях в Chat и Projects
        Chat.ThreadsChanged      += RebuildRecentGlobalChats;
        Projects.ProjectsChanged += RebuildRecentProjects;

        // Также реагируем на прямые изменения коллекций (начальная загрузка)
        Chat.Threads.CollectionChanged    += OnChatThreadsChanged;
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

    // ─── Коллекции для Sidebar ────────────────────────────────────────────────

    /// <summary>Недавние глобальные чаты (все, с прокруткой в сайдбаре).</summary>
    public ObservableCollection<ChatThreadItemViewModel> RecentGlobalChats { get; } = new();

    /// <summary>Недавние проекты (все, с прокруткой в сайдбаре).</summary>
    public ObservableCollection<ProjectCardViewModel> RecentProjects { get; } = new();

    // ─── Команды ──────────────────────────────────────────────────────────────

    public ICommand LogoutCommand        { get; }
    public ICommand ShowDashboardCommand { get; }
    public ICommand ShowProjectsCommand  { get; }
    public ICommand ShowChatCommand      { get; }
    public ICommand ShowDocumentsCommand { get; }
    public ICommand ShowSettingsCommand  { get; }

    public ICommand SelectGlobalChatCommand       { get; }
    public ICommand OpenProjectFromSidebarCommand { get; }

    /// <summary>Кнопка «+» рядом с заголовком «RECENT CHATS» — создаёт новый глобальный тред.</summary>
    public ICommand AddNewGlobalChatCommand { get; }

    /// <summary>Кнопка «+» рядом с заголовком «RECENT PROJECTS» — создаёт новый проект.</summary>
    public ICommand AddNewProjectCommand { get; }

    /// <summary>Переименование чата из Sidebar (вызывается после CommitEdit в ChatThreadItemViewModel).</summary>
    public ICommand RenameChatFromSidebarCommand { get; }

    /// <summary>Переименование проекта из Sidebar (вызывается после CommitEdit в ProjectCardViewModel).</summary>
    public ICommand RenameProjectFromSidebarCommand { get; }

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

    public async Task ApplyAuthAsync(AuthTokenDto authToken, CancellationToken cancellationToken = default)
    {
        _sessionStore.Apply(authToken);
        _projectContextStore.Clear();
        CurrentContent = Dashboard;
        RaiseActiveFlags();
        await Dashboard.LoadAsync(authToken.AccessToken, cancellationToken);
    }

    // ─── Синхронизация Sidebar коллекций ─────────────────────────────────────

    private void RebuildRecentGlobalChats()
    {
        RecentGlobalChats.Clear();
        foreach (var thread in Chat.Threads)
            if (thread.IsGlobal)
                RecentGlobalChats.Add(thread);
    }

    private void RebuildRecentProjects()
    {
        RecentProjects.Clear();
        foreach (var project in Projects.Projects)
            RecentProjects.Add(project);
    }

    private void OnChatThreadsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RebuildRecentGlobalChats();

    private void OnProjectsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RebuildRecentProjects();

    // ─── Кнопки «+» ──────────────────────────────────────────────────────────

    /// <summary>
    /// Создаёт новый глобальный тред с названием = текущая дата/время.
    /// Если Chat ещё не инициализирован — инициализирует его.
    /// </summary>
    private async Task AddNewGlobalChatAsync()
    {
        var title = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
        try
        {
            var dto = await _chatService.CreateGlobalAsync(
                new Entities.Chat.CreateGlobalThreadRequest(title));
            if (dto == null) return;

            // Если треды ещё не загружены — загрузим
            if (Chat.Threads.Count == 0)
                await Chat.InitializeAsync();
            else
            {
                // Вставляем новый тред вручную в начало коллекции Chat.Threads
                // (ChatViewModel не знает об этом создании — обходим через reload)
                await Chat.InitializeAsync();
            }
            RebuildRecentGlobalChats();
        }
        catch { /* silent */ }
    }

    /// <summary>
    /// Создаёт новый проект с названием = текущая дата/время.
    /// </summary>
    private async Task AddNewProjectAsync()
    {
        var name = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
        try
        {
            var dto = await _projectsService.CreateProjectAsync(
                new Entities.Projects.CreateProjectRequest(name, string.Empty));
            if (dto == null) return;

            // Перезагружаем список проектов
            await Projects.InitializeAsync();
            // Принудительно обновляем коллекцию
            await _reloadProjects();
            RebuildRecentProjects();
        }
        catch { /* silent */ }
    }

    // helper — перезагружает Projects без навигации
    private async Task _reloadProjects()
    {
        try { await Projects.LoadProjectsCommand.ExecuteAsync(null); } catch { }
    }

    // ─── Inline rename из Sidebar ─────────────────────────────────────────────

    private async Task RenameChatFromSidebarAsync(ChatThreadItemViewModel? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.EditTitle)) return;
        try
        {
            var dto = await _chatService.RenameAsync(
                item.Id, new Entities.Chat.RenameThreadRequest(item.EditTitle.Trim()));
            if (dto != null) item.Title = dto.Title;
        }
        catch { /* silent */ }
    }

    private async Task RenameProjectFromSidebarAsync(ProjectCardViewModel? card)
    {
        if (card == null || string.IsNullOrWhiteSpace(card.EditName)) return;
        try
        {
            var ok = await _projectsService.UpdateSettingsAsync(
                card.Id,
                new Entities.Projects.UpdateProjectSettingsRequest(
                    card.EditName.Trim(), card.Description, card.AccessMode));
            if (ok) card.Name = card.EditName.Trim();
        }
        catch { /* silent */ }
    }

    // ─── Обработчик кнопки Open ───────────────────────────────────────────────

    private void OnProjectOpened(Guid projectId)
    {
        _ = ShowDocumentsAsync();
    }

    // ─── Навигация ────────────────────────────────────────────────────────────

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
        LogoutRequested?.Invoke();
    }

    private void OnShowSettings()
        => SettingsRequested?.Invoke();

    // ─── Команды Sidebar ──────────────────────────────────────────────────────

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
