/// <summary>
/// ViewModel экрана списка проектов.
/// Поддерживает: загрузку, создание, удаление проектов, открытие настроек.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Projects;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Features.Projects;

public sealed partial class ProjectsViewModel : ObservableObject
{
    private readonly ProjectsService      _projectsService;
    private readonly ProjectContextStore  _projectContext;

    public ObservableCollection<ProjectCardViewModel> Projects { get; } = new();

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _isEmpty;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    // ─── Диалог создания ─────────────────────────────────────────────────────
    [ObservableProperty] private bool _isCreateDialogOpen;
    [ObservableProperty] private CreateProjectDialogViewModel? _createDialog;

    // ─── Диалог настроек ─────────────────────────────────────────────────────
    [ObservableProperty] private bool _isSettingsDialogOpen;
    [ObservableProperty] private ProjectSettingsDialogViewModel? _settingsDialog;

    // ─── Подтверждение удаления ───────────────────────────────────────────────
    [ObservableProperty] private bool   _isDeleteConfirmOpen;
    [ObservableProperty] private string _deleteConfirmName = string.Empty;
    private Guid _pendingDeleteId;

    public event Action<Guid>? ProjectOpened;

    public ProjectsViewModel(
        ProjectsService     projectsService,
        ProjectContextStore projectContext,
        CreateProjectDialogViewModel createDialog)
    {
        _projectsService = projectsService;
        _projectContext  = projectContext;
        _createDialog    = createDialog;

        _createDialog.ProjectCreated += OnProjectCreated;
        _createDialog.Cancelled      += OnCreateCancelled;
    }

    public async Task InitializeAsync() => await LoadAsync();

    // ─── Load ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading    = true;
        HasError     = false;
        ErrorMessage = string.Empty;
        Projects.Clear();
        try
        {
            var list = await _projectsService.GetProjectsAsync();
            if (list == null) return;
            foreach (var dto in list)
            {
                var card = new ProjectCardViewModel(dto);
                card.OpenRequested     += OnOpenProject;
                card.SettingsRequested += OnOpenSettings;
                card.DeleteRequested   += OnRequestDelete;
                Projects.Add(card);
            }
            IsEmpty = Projects.Count == 0;
        }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = ex.Message;
        }
        finally { IsLoading = false; }
    }

    // ─── Create ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenCreateDialog()
    {
        CreateDialog!.Reset();
        IsCreateDialogOpen = true;
    }

    private void OnProjectCreated(ProjectDto dto)
    {
        IsCreateDialogOpen = false;
        var card = new ProjectCardViewModel(new ProjectSummaryDto(
            dto.Id, dto.Name, dto.Description, dto.AccessMode, dto.CreatedAtUtc, dto.FolderCount));
        card.OpenRequested     += OnOpenProject;
        card.SettingsRequested += OnOpenSettings;
        card.DeleteRequested   += OnRequestDelete;
        Projects.Insert(0, card);
        IsEmpty = false;
    }

    private void OnCreateCancelled() => IsCreateDialogOpen = false;

    // ─── Open ─────────────────────────────────────────────────────────────────

    private void OnOpenProject(ProjectCardViewModel card)
    {
        _projectContext.Select(card.Id, card.Name);
        ProjectOpened?.Invoke(card.Id);
    }

    // ─── Settings ─────────────────────────────────────────────────────────────

    private void OnOpenSettings(ProjectCardViewModel card)
    {
        var vm = new ProjectSettingsDialogViewModel(card.Id, _projectsService);
        vm.Closed += () => IsSettingsDialogOpen = false;
        vm.Saved  += () => _ = LoadAsync();

        // Загрузить детали проекта для отображения текущей папки
        _ = LoadProjectDetailsAsync(card.Id, vm);

        SettingsDialog        = vm;
        IsSettingsDialogOpen  = true;
    }

    private async Task LoadProjectDetailsAsync(Guid projectId, ProjectSettingsDialogViewModel vm)
    {
        var dto = await _projectsService.GetProjectByIdAsync(projectId);
        if (dto == null) return;
        // FolderDto недоступен отдельно через API; используем FolderCount как индикатор
        // Полная информация о папке доступна после реализации Documents-сервиса
        vm.LoadFrom(dto, folderAttached: dto.FolderCount > 0);
    }

    // ─── Delete ───────────────────────────────────────────────────────────────

    private void OnRequestDelete(ProjectCardViewModel card)
    {
        _pendingDeleteId    = card.Id;
        DeleteConfirmName   = card.Name;
        IsDeleteConfirmOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        IsDeleteConfirmOpen = false;
        try
        {
            var ok = await _projectsService.DeleteProjectAsync(_pendingDeleteId);
            if (!ok) return;
            for (int i = 0; i < Projects.Count; i++)
                if (Projects[i].Id == _pendingDeleteId)
                { Projects.RemoveAt(i); break; }
            IsEmpty = Projects.Count == 0;
        }
        catch { /* TODO: показать ошибку */ }
    }

    [RelayCommand]
    private void CancelDelete() => IsDeleteConfirmOpen = false;
}
