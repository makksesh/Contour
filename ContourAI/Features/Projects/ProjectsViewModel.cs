using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Chat;
using ContourAI.Entities.Projects;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Features.Projects;

public sealed partial class ProjectsViewModel : ObservableObject
{
    private readonly ProjectsService     _projectsService;
    private readonly ProjectContextStore _projectContextStore;
    private readonly ChatService _chatService;
    private CancellationTokenSource      _cts = new();

    public ObservableCollection<ProjectCardViewModel> Projects { get; } = new();

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _isEmpty;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    [ObservableProperty] private bool   _isCreateDialogOpen;
    [ObservableProperty] private string _newProjectName = string.Empty;

    // ── События ──────────────────────────────────────────────────────────────

    public event Action<Guid>? ProjectOpened;

    /// <summary>
    /// Поднимается после создания, удаления или переименования проекта.
    /// AuthenticatedShellViewModel перестраивает RecentProjects.
    /// </summary>
    public event Action? ProjectsChanged;

    public ProjectsViewModel(
        ProjectsService     projectsService,
        ChatService         chatService,
        ProjectContextStore projectContextStore)
    {
        _projectsService     = projectsService;
        _chatService         = chatService;
        _projectContextStore = projectContextStore;
    }

    public async Task InitializeAsync()
    {
        if (Projects.Count == 0)
            await LoadProjectsAsync();
    }

    public async Task ForceReloadAsync()
        => await LoadProjectsAsync();

    // ── Load ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadProjectsAsync()
    {
        IsLoading = true;
        HasError  = false;
        Projects.Clear();

        try
        {
            var list = await _projectsService.GetProjectsAsync(_cts.Token);
            if (list == null) return;

            foreach (var dto in list)
                AddCard(new ProjectCardViewModel(dto));

            IsEmpty = Projects.Count == 0;
            ProjectsChanged?.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    // ── Create dialog ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenCreateDialog()
    {
        NewProjectName     = string.Empty;
        IsCreateDialogOpen = true;
    }

    [RelayCommand]
    private void CloseCreateDialog()
    {
        IsCreateDialogOpen = false;
        NewProjectName     = string.Empty;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        var name = NewProjectName.Trim();
        if (string.IsNullOrEmpty(name))
            name = $"{DateTime.Now:dd.MM.yyyy HH:mm:ss}";

        IsCreateDialogOpen = false;
        NewProjectName     = string.Empty;
        IsLoading          = true;
        HasError           = false;

        try
        {
            var dto = await _projectsService.CreateProjectAsync(
                new CreateProjectRequest(name, string.Empty), _cts.Token);
            if (dto == null) return;

            // Позиционный конструктор record (не object initializer)
            var summary = new ProjectSummaryDto(
                dto.Id,
                dto.Name,
                dto.Description,
                dto.AccessMode,
                dto.CreatedAtUtc);

            var card = new ProjectCardViewModel(summary);
            AddCard(card);
            Projects.Move(Projects.IndexOf(card), 0);
            IsEmpty = false;
            // Создаём тред чата для нового проекта (костыль: один чат на проект)
            _ = _chatService.CreateInProjectAsync(
                new CreateThreadRequest(dto.Id, $"Chat {dto.Name}"), _cts.Token);
            ProjectsChanged?.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    // ── Open / Delete / Rename ────────────────────────────────────────────────

    private void OnProjectOpenRequested(ProjectCardViewModel card)
    {
        _projectContextStore.Select(card.Id, card.Name);
        ProjectOpened?.Invoke(card.Id);
    }

    private async void OnProjectDeleteRequested(ProjectCardViewModel card)
    {
        try
        {
            var ok = await _projectsService.DeleteProjectAsync(card.Id, _cts.Token);
            if (!ok) return;
            Projects.Remove(card);
            IsEmpty = Projects.Count == 0;
            ProjectsChanged?.Invoke();
        }
        catch { /* silent */ }
    }

    /// <summary>
    /// Переименование проекта через PATCH /api/projects/{id}/settings.
    /// Сервер не имеет отдельного эндпоинта rename — используем settings
    /// с нейтральными дефолтами и оптимистично обновляем Name локально.
    /// </summary>
    private async void OnProjectRenameRequested(ProjectCardViewModel card, string newName)
    {
        // Оптимистичное обновление — сразу меняем имя в UI
        card.Name = newName;
        ProjectsChanged?.Invoke();

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
                    ContextWindowSize:        10),
                _cts.Token);
        }
        catch { /* silent — имя уже обновлено */ }
    }

    // ── Public injection API ──────────────────────────────────────────────────

    /// <summary>
    /// Вставляет карточку проекта в коллекцию с подпиской на события.
    /// Используется Shell при гидрации Sidebar и создании проекта из «+».
    /// </summary>
    public void InjectCard(ProjectCardViewModel card, bool insertAtTop = false)
        => AddCard(card, insertAtTop);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AddCard(ProjectCardViewModel card, bool insertAtTop = false)
    {
        card.OpenRequested   += OnProjectOpenRequested;
        card.DeleteRequested += OnProjectDeleteRequested;
        card.RenameRequested += OnProjectRenameRequested;
        if (insertAtTop)
            Projects.Insert(0, card);
        else
            Projects.Add(card);
    }
}
