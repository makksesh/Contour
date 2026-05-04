/// <summary>
/// ViewModel диалога создания нового проекта.
/// Валидирует поля и выполняет запрос через ProjectsService.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Projects;
using ContourAI.Features.Auth;
using ContourAI.Shared.Api;

namespace ContourAI.Features.Projects;

public sealed class CreateProjectDialogViewModel : ViewModelBase
{
    private readonly ProjectsService _projectsService;
    private string _projectName = string.Empty;
    private string _description = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public CreateProjectDialogViewModel(ProjectsService projectsService)
    {
        _projectsService = projectsService;
        ConfirmCommand = new AsyncRelayCommand(ConfirmAsync);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke());
    }

    public event Action? CancelRequested;
    public event Action<ProjectDto>? ProjectCreated;

    public string ProjectName
    {
        get => _projectName;
        set { SetProperty(ref _projectName, value); RaisePropertyChanged(nameof(CanConfirm)); }
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { SetProperty(ref _errorMessage, value); RaisePropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);

    public bool IsBusy
    {
        get => _isBusy;
        private set { SetProperty(ref _isBusy, value); RaisePropertyChanged(nameof(CanConfirm)); }
    }

    public bool CanConfirm => !IsBusy && !string.IsNullOrWhiteSpace(ProjectName);

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public void Reset()
    {
        ProjectName = string.Empty;
        Description = string.Empty;
        ErrorMessage = string.Empty;
        IsBusy = false;
    }

    private async Task ConfirmAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ProjectName)) return;

        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var request = new CreateProjectRequest(
                ProjectName.Trim(),
                string.IsNullOrWhiteSpace(Description) ? null : Description.Trim());

            var created = await _projectsService.CreateProjectAsync(request, cancellationToken);
            if (created is not null)
                ProjectCreated?.Invoke(created);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось создать проект: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
