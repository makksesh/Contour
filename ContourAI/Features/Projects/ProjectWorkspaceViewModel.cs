/// <summary>
/// ViewModel рабочего пространства проекта.
/// Открывается по клику на проект из SidebarView.
/// Содержит четыре вкладки: Settings, Folder, Documents, Chat.
/// Загружает ProjectDto по projectId через ProjectsService.
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

    /// <summary>Id проекта, полученный от Sidebar при открытии.</summary>
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
    /// Сразу показывает вкладку Settings и асинхронно подгружает полный ProjectDto.
    /// </summary>
    public async Task OpenAsync(Guid projectId, string projectName)
    {
        // Отменяем предыдущую загрузку, если была
        _cts.Cancel();
        _cts = new CancellationTokenSource();

        ProjectId    = projectId;
        ProjectName  = projectName;
        SelectedTabIndex = (int)WorkspaceTab.Settings;
        HasError     = false;
        ErrorMessage = string.Empty;

        // Пересоздаём SettingsViewModel для нового проекта
        var settingsVm = new ProjectSettingsDialogViewModel(projectId, _projectsService);
        settingsVm.Saved   += () => { /* можно показать уведомление */ };
        settingsVm.Closed  += () => BackRequested?.Invoke();
        SettingsViewModel  = settingsVm;

        // Загружаем полный DTO, чтобы заполнить поля настроек
        await LoadProjectAsync(_cts.Token);
    }

    // ─── Загрузка ProjectDto ─────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadProjectAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        HasError  = false;
        try
        {
            var dto = await _projectsService.GetProjectByIdAsync(ProjectId, ct);
            if (dto == null) return;

            // Передаём данные в SettingsViewModel
            if (SettingsViewModel != null)
            {
                SettingsViewModel.SystemPrompt      = string.Empty; // сервер не возвращает prompt в GET /projects/{id}
                SettingsViewModel.HasFolderAttached = dto.FolderCount > 0;
            }

            ProjectName = dto.Name;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = ex.Message;
        }
        finally { IsLoading = false; }
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
