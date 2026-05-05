/// <summary>
/// ViewModel диалога настроек проекта.
/// Загружает текущие значения из ProjectDto, сохраняет через ProjectsService.
/// Управляет папкой: подключить / изменить права / отвязать.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Projects;
using ContourAI.Shared.Api;

namespace ContourAI.Features.Projects;

public sealed partial class ProjectSettingsDialogViewModel : ObservableObject
{
    private readonly ProjectsService _projectsService;
    private readonly Guid            _projectId;

    // ─── Settings fields ──────────────────────────────────────────────────────

    [ObservableProperty] private string _systemPrompt     = string.Empty;
    [ObservableProperty] private int    _maxTokens        = 4096;
    [ObservableProperty] private float  _temperature      = 0.7f;
    [ObservableProperty] private int    _ragTopK          = 5;
    [ObservableProperty] private bool   _useRagContext    = true;
    [ObservableProperty] private int    _contextWindowSize = 10;

    // ─── Folder fields ────────────────────────────────────────────────────────

    [ObservableProperty] private string? _folderPath;
    [ObservableProperty] private bool    _hasFolderAttached;
    [ObservableProperty] private bool    _permRead;
    [ObservableProperty] private bool    _permEdit;
    [ObservableProperty] private bool    _permDelete;

    // ─── State ────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool   _hasError;

    public event Action? Closed;
    public event Action? Saved;

    public ProjectSettingsDialogViewModel(Guid projectId, ProjectsService projectsService)
    {
        _projectId       = projectId;
        _projectsService = projectsService;
    }

    /// <summary>Инициализировать из уже загруженного ProjectDto и текущей FolderDto (если есть).</summary>
    public void LoadFrom(ProjectDto project, FolderDto? folder)
    {
        if (folder != null)
        {
            HasFolderAttached = true;
            FolderPath        = folder.Path;
            PermRead          = folder.Permission.HasFlag(FolderPermission.Read);
            PermEdit          = folder.Permission.HasFlag(FolderPermission.Edit);
            PermDelete        = folder.Permission.HasFlag(FolderPermission.Delete);
        }
        else
        {
            HasFolderAttached = false;
            FolderPath        = null;
        }
    }

    // ─── Save settings ────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        IsBusy     = true;
        HasError   = false;
        ErrorMessage = string.Empty;
        try
        {
            var request = new UpdateProjectSettingsRequest(
                ChatModelEndpointId:       null,
                EmbeddingModelEndpointId:  null,
                SystemPrompt:              SystemPrompt,
                MaxTokens:                 MaxTokens,
                Temperature:               Temperature,
                RagTopK:                   RagTopK,
                UseRagContext:             UseRagContext,
                ContextWindowSize:         ContextWindowSize);

            var ok = await _projectsService.UpdateSettingsAsync(_projectId, request);
            if (!ok)
            {
                ErrorMessage = "Failed to save settings.";
                HasError = true;
                return;
            }
            Saved?.Invoke();
            Closed?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
        }
        finally { IsBusy = false; }
    }

    // ─── Folder: attach ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AttachFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(FolderPath)) return;
        IsBusy = true;
        HasError = false;
        try
        {
            var perm = BuildPermission();
            var dto  = await _projectsService.AddFolderAsync(_projectId, new AddProjectFolderRequest(FolderPath, perm));
            if (dto == null)
            {
                ErrorMessage = "Failed to attach folder.";
                HasError = true;
                return;
            }
            HasFolderAttached = true;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; HasError = true; }
        finally { IsBusy = false; }
    }

    // ─── Folder: change permissions ───────────────────────────────────────────

    [RelayCommand]
    private async Task SaveFolderPermissionsAsync()
    {
        IsBusy = true;
        HasError = false;
        try
        {
            var ok = await _projectsService.ChangeFolderPermissionAsync(_projectId, BuildPermission());
            if (!ok) { ErrorMessage = "Failed to update permissions."; HasError = true; }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; HasError = true; }
        finally { IsBusy = false; }
    }

    // ─── Folder: detach ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DetachFolderAsync()
    {
        IsBusy = true;
        HasError = false;
        try
        {
            var ok = await _projectsService.RemoveFolderAsync(_projectId);
            if (!ok) { ErrorMessage = "Failed to detach folder."; HasError = true; return; }
            HasFolderAttached = false;
            FolderPath        = null;
            PermRead = PermEdit = PermDelete = false;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; HasError = true; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Cancel() => Closed?.Invoke();

    private FolderPermission BuildPermission()
    {
        var p = FolderPermission.None;
        if (PermRead)   p |= FolderPermission.Read;
        if (PermEdit)   p |= FolderPermission.Edit;
        if (PermDelete) p |= FolderPermission.Delete;
        return p;
    }
}
