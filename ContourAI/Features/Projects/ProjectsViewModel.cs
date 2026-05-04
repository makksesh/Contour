/// <summary>
/// ViewModel экрана списка проектов.
/// Загружает проекты через ProjectsService, поддерживает создание и открытие.
/// При выборе проекта обновляет ProjectContextStore.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Projects;
using ContourAI.Features.Auth;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Features.Projects;

public sealed class ProjectsViewModel : ViewModelBase
{
    private readonly ProjectsService _projectsService;
    private readonly ProjectContextStore _projectContextStore;
    private bool _isLoading;
    private bool _isDialogOpen;
    private string _errorMessage = string.Empty;
    private ProjectCardViewModel? _selectedProject;

    public ProjectsViewModel(
        ProjectsService projectsService,
        ProjectContextStore projectContextStore,
        CreateProjectDialogViewModel createProjectDialogViewModel)
    {
        _projectsService = projectsService;
        _projectContextStore = projectContextStore;
        CreateDialog = createProjectDialogViewModel;

        Projects = new ObservableCollection<ProjectCardViewModel>();

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        OpenCreateDialogCommand = new RelayCommand(OpenCreateDialog);
        OpenProjectCommand = new RelayCommand<ProjectCardViewModel>(OpenProject);

        CreateDialog.ProjectCreated += OnProjectCreated;
        CreateDialog.CancelRequested += CloseCreateDialog;
    }

    public ObservableCollection<ProjectCardViewModel> Projects { get; }

    public CreateProjectDialogViewModel CreateDialog { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set { SetProperty(ref _isLoading, value); RaisePropertyChanged(nameof(IsEmpty)); }
    }

    public bool IsDialogOpen
    {
        get => _isDialogOpen;
        private set => SetProperty(ref _isDialogOpen, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { SetProperty(ref _errorMessage, value); RaisePropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);

    /// <summary>True — список пуст и не идёт загрузка.</summary>
    public bool IsEmpty => !IsLoading && Projects.Count == 0 && !HasError;

    public ProjectCardViewModel? SelectedProject
    {
        get => _selectedProject;
        set => SetProperty(ref _selectedProject, value);
    }

    public ICommand LoadCommand { get; }
    public ICommand OpenCreateDialogCommand { get; }
    public ICommand OpenProjectCommand { get; }

    /// <summary>Вызывается при переключении на экран Projects из shell.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Projects.Count == 0)
            await LoadAsync(cancellationToken);
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = string.Empty;
        IsLoading = true;
        Projects.Clear();
        RaisePropertyChanged(nameof(IsEmpty));

        try
        {
            var list = await _projectsService.GetProjectsAsync(cancellationToken);
            if (list is null) return; // 401/403 — HandleUnauthorized уже вызван

            foreach (var p in list)
                Projects.Add(new ProjectCardViewModel(p.Id, p.Name, p.Description, FormatRelativeTime(p.UpdatedAtUtc)));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка загрузки проектов: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            RaisePropertyChanged(nameof(IsEmpty));
        }
    }

    private void OpenCreateDialog()
    {
        CreateDialog.Reset();
        IsDialogOpen = true;
    }

    private void CloseCreateDialog()
    {
        IsDialogOpen = false;
    }

    private async void OnProjectCreated(ProjectDto dto)
    {
        IsDialogOpen = false;
        // Добавляем новый проект в начало списка без полной перезагрузки
        Projects.Insert(0, new ProjectCardViewModel(
            dto.Id, dto.Name, dto.Description, "Только что"));
        RaisePropertyChanged(nameof(IsEmpty));
        // Сразу открываем созданный проект
        OpenProject(Projects[0]);
    }

    private void OpenProject(ProjectCardViewModel? card)
    {
        if (card is null) return;
        _projectContextStore.Select(card.Id, card.Name);
        SelectedProject = card;
    }

    private static string FormatRelativeTime(DateTime updatedAtUtc)
    {
        var delta = DateTime.UtcNow - updatedAtUtc;
        if (delta.TotalMinutes < 1) return "Только что";
        if (delta.TotalHours < 1) return $"{Math.Max(1, (int)delta.TotalMinutes)} мин. назад";
        if (delta.TotalDays < 1) return $"{Math.Max(1, (int)delta.TotalHours)} ч. назад";
        return $"{Math.Max(1, (int)delta.TotalDays)} дн. назад";
    }
}
