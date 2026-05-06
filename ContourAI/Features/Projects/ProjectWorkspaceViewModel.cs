/// <summary>
/// ViewModel рабочего пространства проекта.
/// Открывается по клику на проект из SidebarView.
/// Содержит четыре вкладки: Settings, Folder, Documents, Chat.
///
/// Намеренно НЕ вызывает GET /api/projects/{id} — этот endpoint
/// падает с 500 из-за отсутствия IMapper на сервере.
/// Имя проекта уже известно из Sidebar (передаётся в OpenAsync).
/// FolderCount определяется косвенно через GET /api/projects/{id}/settings
/// (если сервер вернёт данные — папка точно привязана или нет —
///  используем FolderCount из SidebarDto, переданный снаружи).
///
/// Загружает ProjectSettingsDto через GET /api/projects/{id}/settings
/// и заполняет поля формы Settings.
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

    // ─── Идентификация ─────────────────────────────────────────────────────────

    public Guid ProjectId { get; private set; }

    // ─── Заголовок ─────────────────────────────────────────────────────────────

    [ObservableProperty] private string _projectName = string.Empty;

    // ─── Вкладки ───────────────────────────────────────────────────────────────

    [ObservableProperty] private int _selectedTabIndex = (int)WorkspaceTab.Settings;

    // ─── Состояния загрузки ────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    // ─── Вложенные ViewModel ───────────────────────────────────────────────────

    /// <summary>
    /// ViewModel настроек проекта.
    /// Пересоздаётся при каждом открытии нового проекта.
    /// </summary>
    [ObservableProperty] private ProjectSettingsDialogViewModel? _settingsViewModel;

    // ─── Событие «назад» ───────────────────────────────────────────────────────

    /// <summary>Пользователь нажал «Назад» — Shell должен вернуть предыдущий экран.</summary>
    public event Action? BackRequested;

    public ProjectWorkspaceViewModel(ProjectsService projectsService)
    {
        _projectsService = projectsService;
    }

    // ─── Инициализация ─────────────────────────────────────────────────────────

    /// <summary>
    /// Открывает проект по id и имени (известны из Sidebar без доп. запроса).
    /// Параметр folderCount передаётся из ProjectSummaryDto, полученного
    /// при загрузке списка проектов — GET /api/projects (работает).
    /// Загружает только GET /api/projects/{id}/settings.
    /// </summary>
    public async Task OpenAsync(Guid projectId, string projectName, int folderCount = 0)
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();

        ProjectId        = projectId;
        ProjectName      = projectName;
        SelectedTabIndex = (int)WorkspaceTab.Settings;
        HasError         = false;
        ErrorMessage     = string.Empty;

        // Пересоздаём SettingsViewModel с дефолтами
        var settingsVm           = new ProjectSettingsDialogViewModel(projectId, _projectsService);
        settingsVm.Saved        += () => { };
        settingsVm.Closed       += () => BackRequested?.Invoke();
        settingsVm.HasFolderAttached = folderCount > 0;
        SettingsViewModel        = settingsVm;

        await LoadSettingsAsync(_cts.Token);
    }

    // ─── Загрузка ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Загружает GET /api/projects/{id}/settings и заполняет поля формы.
    /// Только этот endpoint — GET /api/projects/{id} исключён намеренно
    /// (падает с 500 IMapper на сервере).
    /// </summary>
    [RelayCommand]
    private async Task LoadSettingsAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        HasError  = false;
        try
        {
            var settings = await _projectsService.GetProjectSettingsAsync(ProjectId, ct);

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

    // ─── Навигация по вкладкам ─────────────────────────────────────────────────

    [RelayCommand]
    private void SelectTab(WorkspaceTab tab)
        => SelectedTabIndex = (int)tab;

    // ─── Кнопка «Назад» ────────────────────────────────────────────────────────

    [RelayCommand]
    private void GoBack()
        => BackRequested?.Invoke();
}
