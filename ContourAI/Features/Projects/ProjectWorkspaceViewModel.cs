/// <summary>
/// ViewModel рабочего пространства проекта.
/// Открывается по клику на проект из SidebarView.
/// Содержит четыре вкладки: Settings, Folder, Documents, Chat.
/// Загружает ProjectSettingsDto через GET /api/projects/{id}/settings
/// и ProjectDto через GET /api/projects/{id} параллельно.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Projects;
using ContourAI.Shared.Api;

namespace ContourAI.Features.Projects;

/// <summary>
/// Индекс активной вкладки workspace.
/// </summary>
public enum WorkspaceTab
{
    Settings  = 0,
    Folder    = 1,
    Documents = 2,
    Chat      = 3
}

public sealed partial class ProjectWorkspaceViewModel : ObservableObject
{
    private readonly ProjectsService _projectsService;
    private CancellationTokenSource  _cts = new();

    // ─── Идентификация ───────────────────────────────────────────────────────

    public Guid ProjectId { get; private set; }

    // ─── Заголовок ───────────────────────────────────────────────────────────

    [ObservableProperty] private string _projectName = string.Empty;

    // ─── Вкладки ─────────────────────────────────────────────────────────────

    [ObservableProperty] private int _selectedTabIndex = (int)WorkspaceTab.Settings;

    // ─── Состояния загрузки ──────────────────────────────────────────────────

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    // ─── Вложенные ViewModel ─────────────────────────────────────────────────

    /// <summary>
    /// ViewModel настроек проекта.
    /// Пересоздаётся при каждом открытии нового проекта.
    /// </summary>
    [ObservableProperty] private ProjectSettingsDialogViewModel? _settingsViewModel;

    // ─── Событие «назад» ─────────────────────────────────────────────────────

    /// <summary>Пользователь нажал «Назад» — Shell должен вернуть предыдущий экран.</summary>
    public event Action? BackRequested;

    public ProjectWorkspaceViewModel(ProjectsService projectsService)
    {
        _projectsService = projectsService;
    }

    // ─── Инициализация ────────────────────────────────────────────────────────

    /// <summary>
    /// Открывает проект по id и имени (известны из Sidebar без доп. запроса).
    /// Параллельно загружает GET /api/projects/{id}
    /// и GET /api/projects/{id}/settings, затем применяет значения.
    /// </summary>
    public async Task OpenAsync(Guid projectId, string projectName)
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();

        ProjectId        = projectId;
        ProjectName      = projectName;
        SelectedTabIndex = (int)WorkspaceTab.Settings;
        HasError         = false;
        ErrorMessage     = string.Empty;

        // Пересоздаём SettingsViewModel с дефолтами
        var settingsVm    = new ProjectSettingsDialogViewModel(projectId, _projectsService);
        settingsVm.Saved  += () => { };
        settingsVm.Closed += () => BackRequested?.Invoke();
        SettingsViewModel  = settingsVm;

        await LoadProjectAsync(_cts.Token);
    }

    // ─── Загрузка ────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadProjectAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        HasError  = false;
        try
        {
            // Параллельный запрос: GET /projects/{id} + GET /projects/{id}/settings
            var projectTask  = _projectsService.GetProjectByIdAsync(ProjectId, ct);
            var settingsTask = _projectsService.GetProjectSettingsAsync(ProjectId, ct);
            await Task.WhenAll(projectTask, settingsTask);

            var dto      = projectTask.Result;
            var settings = settingsTask.Result;

            if (dto != null)
            {
                ProjectName = dto.Name;
                if (SettingsViewModel != null)
                    SettingsViewModel.HasFolderAttached = dto.FolderCount > 0;
            }

            // Заполняем поля настроек реальными значениями с сервера
            if (settings != null && SettingsViewModel != null)
                ApplySettings(settings, SettingsViewModel);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = ex.Message;
        }
        finally { IsLoading = false; }
    }

    /// <summary>
    /// Переносит значения из ProjectSettingsDto в поля SettingsViewModel.
    /// </summary>
    private static void ApplySettings(ProjectSettingsDto dto, ProjectSettingsDialogViewModel vm)
    {
        vm.SystemPrompt      = dto.SystemPrompt;
        vm.MaxTokens         = dto.MaxTokens;
        vm.Temperature       = dto.Temperature;
        vm.RagTopK           = dto.RagTopK;
        vm.UseRagContext     = dto.UseRagContext;
        vm.ContextWindowSize = dto.ContextWindowSize;
    }

    // ─── Навигация по вкладкам ───────────────────────────────────────────────

    [RelayCommand]
    private void SelectTab(WorkspaceTab tab)
        => SelectedTabIndex = (int)tab;

    // ─── Кнопка «Назад» ──────────────────────────────────────────────────────

    [RelayCommand]
    private void GoBack()
        => BackRequested?.Invoke();
}
