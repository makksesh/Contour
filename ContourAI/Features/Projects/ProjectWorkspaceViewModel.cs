/// <summary>
/// ViewModel рабочего пространства проекта.
/// Открывается по клику на проект из SidebarView.
/// Вкладки: Settings, Folder, Documents, Chat, Sync, RagSearch.
///
/// Chat-вкладка (lazy):
///   При первом переключении на WorkspaceTab.Chat вызывается InitializeChatAsync:
///   - GET /api/chat/projects/{id}/threads
///   - Нет тредов → HasProjectChat = false (показывается кнопка "Начать чат")
///   - Есть треды  → берётся первый, создаётся ChatViewModel, загружается история
///   - StartChatCommand → POST /api/chat/threads → InitializeChatAsync повторно
///
/// RagSearch-вкладка (lazy):
///   При первом переключении вызывается RagSearchViewModel.SetProject(projectId).
///   Дальнейшая ленивая инициализация не нужна — VM безсостоятельна до первой команды SearchCommand.
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
using ContourAI.Features.Workspace;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Features.Projects;

public enum WorkspaceTab
{
    Settings  = 0,
    Documents = 1,
    Chat      = 2,
    Sync      = 3,
    RagSearch = 4
}

public sealed partial class ProjectWorkspaceViewModel : ObservableObject
{
    private readonly ProjectsService            _projectsService;
    private readonly ChatService                _chatService;
    private readonly ProjectContextStore        _projectContextStore;
    private readonly WorkspaceSyncViewModel     _workspaceSyncViewModel;
    private readonly AgentTasksViewModel        _agentTasksViewModel;
    private readonly ChangeSetReviewViewModel   _changeSetReviewViewModel;
    private CancellationTokenSource             _cts = new();
    private bool                                _documentsLoaded;
    private bool                                _chatInitialized;
    private bool                                _syncInitialized;
    private bool                                _useRagContext;
    private int                                 _ragTopK = 5;

    public Guid ProjectId { get; private set; }

    [ObservableProperty] private string _projectName = string.Empty;
    [ObservableProperty] private int _selectedTabIndex = (int)WorkspaceTab.Settings;
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private ProjectSettingsDialogViewModel? _settingsViewModel;

    public ProjectDocumentsViewModel DocumentsViewModel { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowChatView))]
    [NotifyPropertyChangedFor(nameof(ShowStartChatButton))]
    private ChatViewModel? _projectChatViewModel;

    [ObservableProperty] private bool _isChatLoading;

    public bool ShowChatView        => ProjectChatViewModel is not null;
    public bool ShowStartChatButton => ProjectChatViewModel is null && !IsChatLoading;

    public RagSearchViewModel RagSearchViewModel { get; }

    [ObservableProperty] private int _syncSubPanelIndex;

    public WorkspaceSyncViewModel   SyncViewModel       => _workspaceSyncViewModel;
    public AgentTasksViewModel      AgentTasksViewModel => _agentTasksViewModel;
    public ChangeSetReviewViewModel ReviewViewModel     => _changeSetReviewViewModel;

    public event Action? BackRequested;
    public event Action? ProjectDeleted;

    public ProjectWorkspaceViewModel(
        ProjectsService           projectsService,
        ChatService               chatService,
        ProjectContextStore       projectContextStore,
        ProjectDocumentsViewModel documentsViewModel,
        WorkspaceSyncViewModel    workspaceSyncViewModel,
        AgentTasksViewModel       agentTasksViewModel,
        ChangeSetReviewViewModel  changeSetReviewViewModel,
        RagSearchViewModel        ragSearchViewModel)
    {
        _projectsService          = projectsService;
        _chatService              = chatService;
        _projectContextStore      = projectContextStore;
        DocumentsViewModel        = documentsViewModel;
        _workspaceSyncViewModel   = workspaceSyncViewModel;
        _agentTasksViewModel      = agentTasksViewModel;
        _changeSetReviewViewModel = changeSetReviewViewModel;
        RagSearchViewModel        = ragSearchViewModel;

        _workspaceSyncViewModel.NavigateToAgentTasksRequested += OnNavigateToAgentTasks;
        _agentTasksViewModel.NavigateToReviewRequested        += OnNavigateToReview;
        _changeSetReviewViewModel.BackRequested               += OnReviewBack;
    }

    public async Task OpenAsync(Guid projectId, string projectName)
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        _documentsLoaded  = false;
        _chatInitialized  = false;
        _syncInitialized  = false;
        _useRagContext    = false;
        _ragTopK          = 5;
        SyncSubPanelIndex = 0;

        ProjectId        = projectId;
        ProjectName      = projectName;
        SelectedTabIndex = (int)WorkspaceTab.Settings;
        HasError         = false;
        ErrorMessage     = string.Empty;

        ProjectChatViewModel = null;
        RagSearchViewModel.SetProject(projectId);

        var settingsVm            = new ProjectSettingsDialogViewModel(projectId, _projectsService);
        settingsVm.Closed        += () => BackRequested?.Invoke();
        settingsVm.Deleted += () => ProjectDeleted?.Invoke();
        SettingsViewModel = settingsVm;

        await LoadSettingsAsync(_cts.Token);
    }

    [RelayCommand]
    private async Task LoadSettingsAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        HasError  = false;
        try
        {
            var settings = await _projectsService.GetProjectSettingsAsync(ProjectId, ct);
            if (settings != null && SettingsViewModel != null)
                ApplySettings(settings, SettingsViewModel, this);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    private static void ApplySettings(ProjectSettingsDto dto, ProjectSettingsDialogViewModel vm, ProjectWorkspaceViewModel owner)
    {
        vm.SystemPrompt      = dto.SystemPrompt;
        vm.MaxTokens         = dto.MaxTokens;
        vm.Temperature       = dto.Temperature;
        vm.RagTopK           = dto.RagTopK;
        vm.UseRagContext     = dto.UseRagContext;
        vm.ContextWindowSize = dto.ContextWindowSize;

        owner._useRagContext = dto.UseRagContext;
        owner._ragTopK       = dto.RagTopK;
    }

    [RelayCommand]
    private void SelectTab(WorkspaceTab tab)
    {
        SelectedTabIndex = (int)tab;

        if (tab == WorkspaceTab.Documents && !_documentsLoaded)
        {
            _documentsLoaded = true;
            _ = DocumentsViewModel.LoadAsync(ProjectId, _cts.Token);
        }

        if (tab == WorkspaceTab.Chat && !_chatInitialized)
        {
            _chatInitialized = true;
            _ = InitializeChatAsync(_cts.Token);
        }

        if (tab == WorkspaceTab.Sync && !_syncInitialized)
        {
            _syncInitialized = true;
            _ = _workspaceSyncViewModel.InitializeAsync(ProjectId, _cts.Token);
            _ = _agentTasksViewModel.InitializeAsync(_cts.Token);
        }

        if (tab == WorkspaceTab.RagSearch)
            RagSearchViewModel.SetProject(ProjectId);
    }

    private async Task InitializeChatAsync(CancellationToken ct)
    {
        IsChatLoading        = true;
        ProjectChatViewModel = null;
        try
        {
            var threads = await _chatService.GetThreadsByProjectAsync(ProjectId, ct);
            if (threads == null || threads.Count == 0)
                return;

            var firstThread = threads.First();
            var vm = BuildChatViewModel();
            await vm.InitializeAsync();
            await vm.OpenThreadByIdAsync(firstThread.Id);

            ProjectChatViewModel = vm;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsChatLoading = false; }
    }

    [RelayCommand]
    private async Task StartChatAsync()
    {
        IsChatLoading = true;
        HasError      = false;
        try
        {
            var title = $"Чат {DateTime.Now:dd.MM.yyyy HH:mm}";
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

    private ChatViewModel BuildChatViewModel()
        => new(_chatService, _projectContextStore,
               projectId:      ProjectId,
               headerTitle:    ProjectName,
               showBackButton: false,
               isRagEnabled:   _useRagContext,
               ragTopK:        _ragTopK);

    private void OnNavigateToAgentTasks()
    {
        SyncSubPanelIndex = 1;
        _ = _agentTasksViewModel.InitializeAsync(_cts.Token);
    }

    private void OnNavigateToReview(AgentTaskViewModel taskVm)
    {
        SyncSubPanelIndex = 2;
        if (taskVm.ChangeSetId.HasValue)
            _ = _changeSetReviewViewModel.LoadAsync(
                taskVm.WorkspaceId, taskVm.ChangeSetId.Value, _cts.Token);
    }

    private void OnReviewBack() => SyncSubPanelIndex = 1;

    [RelayCommand]
    private void GoBack()
    {
        _agentTasksViewModel.Cleanup();
        BackRequested?.Invoke();
    }
}
