using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Projects;
using ContourAI.Shared.Api;

namespace ContourAI.Features.Projects;

public sealed partial class CreateProjectDialogViewModel : ObservableObject
{
    private readonly ProjectsService _projectsService;

    [ObservableProperty] private string _name        = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private int    _accessModeIndex; // 0=Private, 1=Shared
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public event Action<ProjectDto>? ProjectCreated;
    public event Action?             Cancelled;

    public CreateProjectDialogViewModel(ProjectsService projectsService)
    {
        _projectsService = projectsService;
    }

    public void Reset()
    {
        Name             = string.Empty;
        Description      = string.Empty;
        AccessModeIndex  = 0;
        IsBusy           = false;
        HasError         = false;
        ErrorMessage     = string.Empty;
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Укажите название проекта.";
            HasError     = true;
            return;
        }

        IsBusy       = true;
        HasError     = false;
        ErrorMessage = string.Empty;
        try
        {
            var request = new CreateProjectRequest(
                Name:        Name.Trim(),
                Description: string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                AccessMode:  AccessModeIndex == 1 ? ProjectAccessMode.Shared : ProjectAccessMode.Private);

            var dto = await _projectsService.CreateProjectAsync(request);
            if (dto == null)
            {
                ErrorMessage = "Не удалось создать проект. Попробуйте ещё раз.";
                HasError     = true;
                return;
            }
            ProjectCreated?.Invoke(dto);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError     = true;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
}
