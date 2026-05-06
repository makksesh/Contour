/// <summary>
/// ViewModel рабочего пространства проекта.
/// Открывается по клику на проект из SidebarView.
/// Вкладки: Settings, Folder, Documents, Chat.
///
/// Documents: при переключении на вкладку Documents
/// загружается список документов (один раз за жизнь проекта,
/// Refresh — вручную по кнопке).
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

/// <summary>Индекс активной вкладки workspace.</summary>
public enum WorkspaceTab
{
    Settings  = 0,
    Folder    = 1,
    Documents = 2,
    Chat      = 3
}

public sealed partial class ProjectWorkspaceViewModel : ObservableObject
{
    private readonly ProjectsService  _projectsService;
    private CancellationTokenSource   _cts = new();
    private bool                      _documentsLoaded;

    // ─── Идентификация ─────────────────────────────────────────────────────────────

    public Guid ProjectId { get; private set; }

    // ─── Заголовок ───────────────────────────────────────────────────────────────────

    [ObservableProperty] private string _projectName = string.Empty;

    // ─── Вкладки ─────────────────────────────────────────────────────────────────────

    [ObservableProperty] private int _selectedTabIndex = (int)WorkspaceTab.Settings;

    // ─── Состояния загрузки ────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    // ─── Вложенные ViewModel ───────────────────────────────────────────────────────

    [ObservableProperty] private ProjectSettingsDialogViewModel? _settingsViewModel;

    /// <summary>ViewModel вкладки Documents. Создаётся один раз, живёт всё время Workspace.</summary>
    public ProjectDocumentsViewModel DocumentsViewModel { get; }

    // ─── Событие «назад» ────────────────────────────────────────────────────────

    public event Action? BackRequested;

    public ProjectWorkspaceViewModel(
        ProjectsService         projectsService,
        ProjectDocumentsViewModel documentsViewModel)
    {
        _projectsService  = projectsService;
        DocumentsViewModel = documentsViewModel;
    }

    // ─── Инициализация ──────────────────────────────────────────────────────────────

    public async Task OpenAsync(Guid projectId, string projectName, int folderCount = 0)
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        _documentsLoaded = false;

        ProjectId        = projectId;
        ProjectName      = projectName;
        SelectedTabIndex = (int)WorkspaceTab.Settings;
        HasError         = false;
        ErrorMessage     = string.Empty;

        var settingsVm           = new ProjectSettingsDialogViewModel(projectId, _projectsService);
        settingsVm.Saved        += () => { };
        settingsVm.Closed       += () => BackRequested?.Invoke();
        settingsVm.HasFolderAttached = folderCount > 0;
        SettingsViewModel        = settingsVm;

        await LoadSettingsAsync(_cts.Token);
    }

    // ─── Загрузка настроек ─────────────────────────────────────────────────────────

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
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    private static void ApplySettings(ProjectSettingsDto dto, ProjectSettingsDialogViewModel vm)
    {
        vm.SystemPrompt      = dto.SystemPrompt;
        vm.MaxTokens         = dto.MaxTokens;
        vm.Temperature       = dto.Temperature;
        vm.RagTopK           = dto.RagTopK;
        vm.UseRagContext     = dto.UseRagContext;
        vm.ContextWindowSize = dto.ContextWindowSize;
    }

    // ─── Навигация по вкладкам ──────────────────────────────────────────────────────

    [RelayCommand]
    private void SelectTab(WorkspaceTab tab)
    {
        SelectedTabIndex = (int)tab;

        // Ленивая загрузка Documents — только при первом открытии вкладки
        if (tab == WorkspaceTab.Documents && !_documentsLoaded)
        {
            _documentsLoaded = true;
            _ = DocumentsViewModel.LoadAsync(ProjectId, _cts.Token);
        }
    }

    // ─── Кнопка «Назад» ────────────────────────────────────────────────────────────

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();
}
