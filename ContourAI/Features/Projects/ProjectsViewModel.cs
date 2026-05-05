/// <summary>
/// ViewModel экрана списка проектов.
/// Загружает проекты через ProjectsService.
/// После создания проекта поднимает ProjectsChanged — AuthenticatedShellViewModel
/// подписывается и перестраивает RecentProjects в Sidebar.
/// Поддерживает инлайн-переименование через ProjectCardViewModel.RenameRequested.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Projects;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Features.Projects;

public sealed partial class ProjectsViewModel : ObservableObject
{
    private readonly ProjectsService     _projectsService;
    private readonly ProjectContextStore _projectContextStore;
    private CancellationTokenSource      _cts = new();

    public ObservableCollection<ProjectCardViewModel> Projects { get; } = new();

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _isEmpty;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    // ── Создание нового проекта (диалог) ──────────────────────────────────────
    [ObservableProperty] private bool   _isCreateDialogOpen;
    [ObservableProperty] private string _newProjectName = string.Empty;

    // ── События ──────────────────────────────────────────────────────────────

    /// <summary>Пользователь нажал Open на карточке проекта.</summary>
    public event Action<Guid>? ProjectOpened;

    /// <summary>
    /// Поднимается после создания или удаления проекта.
    /// AuthenticatedShellViewModel обновляет RecentProjects в Sidebar.
    /// </summary>
    public event Action? ProjectsChanged;

    public ProjectsViewModel(
        ProjectsService     projectsService,
        ProjectContextStore projectContextStore)
    {
        _projectsService     = projectsService;
        _projectContextStore = projectContextStore;
    }

    // ── Initialize ────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        if (Projects.Count == 0)
            await LoadProjectsAsync();
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadProjectsAsync()
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
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    // ── Create project dialog ─────────────────────────────────────────────────

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

    // ── Create project ────────────────────────────────────────────────────────

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

            var summary = new ProjectSummaryDto
            {
                Id          = dto.Id,
                Name        = dto.Name,
                Description = dto.Description,
                AccessMode  = dto.AccessMode,
                CreatedAtUtc = dto.CreatedAtUtc,
                FolderCount = 0
            };
            var card = new ProjectCardViewModel(summary);
            AddCard(card);
            Projects.Move(Projects.IndexOf(card), 0); // в начало
            IsEmpty = false;
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

    private async void OnProjectRenameRequested(ProjectCardViewModel card, string newName)
    {
        try
        {
            var ok = await _projectsService.UpdateSettingsAsync(
                card.Id,
                new UpdateProjectSettingsRequest(newName, card.Description, card.AccessMode),
                _cts.Token);
            if (ok)
            {
                card.Name = newName;
                ProjectsChanged?.Invoke();
            }
        }
        catch { /* silent */ }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AddCard(ProjectCardViewModel card)
    {
        card.OpenRequested    += OnProjectOpenRequested;
        card.DeleteRequested  += OnProjectDeleteRequested;
        card.RenameRequested  += OnProjectRenameRequested;
        Projects.Add(card);
    }
}
