/// <summary>
/// ViewModel авторизованного shell-экрана.
/// Управляет навигацией: Projects, Chat, Documents.
/// Предоставляет RecentGlobalChats и RecentProjects для SidebarView.
/// SelectGlobalChatCommand — открыть конкретный глобальный тред из сайдбара.
/// OpenProjectFromSidebarCommand — открыть проект из сайдбара.
/// ShowSettingsCommand — заглушка для будущего экрана настроек.
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
    private object? _currentContent;

    public AuthenticatedShellViewModel(
        ConnectionSettingsStore connectionSettingsStore,
        AuthSessionStore        sessionStore,
        SessionAuthService      sessionAuthService,
        ProjectContextStore     projectContextStore,
        DashboardViewModel      dashboardViewModel,
        ProjectsViewModel       projectsViewModel,
        ChatViewModel           chatViewModel,
        DocumentsViewModel      documentsViewModel)
    {
        _connectionSettingsStore = connectionSettingsStore;
        _sessionStore            = sessionStore;
        _sessionAuthService      = sessionAuthService;
        _projectContextStore     = projectContextStore;

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

        SelectGlobalChatCommand      = new AsyncRelayCommand<ChatThreadItemViewModel>(SelectGlobalChatAsync);
        OpenProjectFromSidebarCommand = new AsyncRelayCommand<ProjectCardViewModel>(OpenProjectFromSidebarAsync);

        // Кнопка "Open" на карточке проекта — переходим на Documents этого проекта
        Projects.ProjectOpened += OnProjectOpened;

        // Синхронизация RecentGlobalChats с Chat.Threads
        Chat.Threads.CollectionChanged += OnChatThreadsChanged;

        // Синхронизация RecentProjects с Projects.Projects
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

        // Старт — показываем dashboard
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

    /// <summary>Открыть конкретный глобальный тред из Sidebar.</summary>
    public ICommand SelectGlobalChatCommand { get; }

    /// <summary>Открыть проект из Sidebar (переход на Documents).</summary>
    public ICommand OpenProjectFromSidebarCommand { get; }

    public string Username        => _sessionStore.CurrentUsername;
    public string ServerIpDisplay => _connectionSettingsStore.ServerIpDisplay;

    /// <summary>Текущий отображаемый раздел в центральной области shell.</summary>
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
    /// Применяет токен в AuthSessionStore и загружает dashboard.
    /// </summary>
    public async Task ApplyAuthAsync(AuthTokenDto authToken, CancellationToken cancellationToken = default)
    {
        _sessionStore.Apply(authToken);
        _projectContextStore.Clear();
        CurrentContent = Dashboard;
        RaiseActiveFlags();
        await Dashboard.LoadAsync(authToken.AccessToken, cancellationToken);
    }

    // ─── Синхронизация Sidebar коллекций ─────────────────────────────────────

    /// <summary>
    /// Пересобирает RecentGlobalChats из Chat.Threads (только IsGlobal == true).
    /// </summary>
    private void RebuildRecentGlobalChats()
    {
        RecentGlobalChats.Clear();
        foreach (var thread in Chat.Threads)
            if (thread.IsGlobal)
                RecentGlobalChats.Add(thread);
    }

    /// <summary>
    /// Пересобирает RecentProjects из Projects.Projects.
    /// </summary>
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

    // ─── Обработчик кнопки Open ───────────────────────────────────────────────

    /// <summary>
    /// Вызывается когда пользователь нажал Open на карточке проекта.
    /// ProjectContextStore уже обновлён внутри ProjectsViewModel.OnOpenProject().
    /// Переходим на Documents и загружаем документы выбранного проекта.
    /// </summary>
    private void OnProjectOpened(Guid projectId)
    {
        var _ = ShowDocumentsAsync();
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

    /// <summary>
    /// Открывает Chat и активирует выбранный тред.
    /// </summary>
    private async Task SelectGlobalChatAsync(ChatThreadItemViewModel? thread)
    {
        if (thread == null) return;
        CurrentContent = Chat;
        RaiseActiveFlags();
        // Убеждаемся что треды загружены, затем открываем нужный
        if (Chat.Threads.Count == 0)
            await Chat.InitializeAsync();
        await Chat.OpenThreadByIdAsync(thread.Id);
    }

    /// <summary>
    /// Открывает проект из сайдбара (устанавливает контекст и переходит на Documents).
    /// </summary>
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
