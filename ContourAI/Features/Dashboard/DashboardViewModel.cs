/// <summary>
/// ViewModel дашборда второй фазы UI.
/// Загружает последние проекты, чаты и документы через реальный API dashboard.
/// Проект: DevAssistant / ContourAI.
/// </summary>
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using ContourAI.Entities.Dashboard;
using ContourAI.Features.Auth;
using ContourAI.Shared.Api;

namespace ContourAI.Features.Dashboard;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly DashboardService _dashboardService;
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    public DashboardViewModel(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
        RecentProjects = new ObservableCollection<RecentItemViewModel>();
        RecentChats = new ObservableCollection<RecentItemViewModel>();
        RecentDocuments = new ObservableCollection<RecentItemViewModel>();

        RecentProjects.CollectionChanged += OnRecentCollectionChanged;
        RecentChats.CollectionChanged += OnRecentCollectionChanged;
        RecentDocuments.CollectionChanged += OnRecentCollectionChanged;
    }

    public ObservableCollection<RecentItemViewModel> RecentProjects { get; }

    public ObservableCollection<RecentItemViewModel> RecentChats { get; }

    public ObservableCollection<RecentItemViewModel> RecentDocuments { get; }

    public bool HasProjects => RecentProjects.Count > 0;

    public bool HasChats => RecentChats.Count > 0;

    public bool HasDocuments => RecentDocuments.Count > 0;

    public bool IsProjectsEmpty => RecentProjects.Count == 0;

    public bool IsChatsEmpty => RecentChats.Count == 0;

    public bool IsDocumentsEmpty => RecentDocuments.Count == 0;

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                RaisePropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task LoadAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ErrorMessage = string.Empty;
        IsLoading = true;

        try
        {
            var response = await _dashboardService.GetRecentAsync(accessToken, cancellationToken);
            ApplyItems(RecentProjects, response?.Projects, "Проект");
            ApplyItems(RecentChats, response?.Chats, "Чат");
            ApplyItems(RecentDocuments, response?.Documents, "Документ");
            RaiseDashboardCollectionFlags();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка загрузки dashboard: {ex.Message}";
            Clear();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Clear()
    {
        RecentProjects.Clear();
        RecentChats.Clear();
        RecentDocuments.Clear();
        ErrorMessage = string.Empty;
        IsLoading = false;
        RaiseDashboardCollectionFlags();
    }

    private void OnRecentCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaiseDashboardCollectionFlags();
    }

    private void RaiseDashboardCollectionFlags()
    {
        RaisePropertyChanged(nameof(HasProjects));
        RaisePropertyChanged(nameof(HasChats));
        RaisePropertyChanged(nameof(HasDocuments));
        RaisePropertyChanged(nameof(IsProjectsEmpty));
        RaisePropertyChanged(nameof(IsChatsEmpty));
        RaisePropertyChanged(nameof(IsDocumentsEmpty));
    }

    private static void ApplyItems(
        ObservableCollection<RecentItemViewModel> target,
        IReadOnlyList<RecentItemResponse>? source,
        string kind)
    {
        target.Clear();

        if (source is null)
        {
            return;
        }

        foreach (var item in source)
        {
            target.Add(new RecentItemViewModel(item.Title, kind, FormatRelativeTime(item.UpdatedAt)));
        }
    }

    private static string FormatRelativeTime(DateTime updatedAtUtc)
    {
        var delta = DateTime.UtcNow - updatedAtUtc;
        if (delta.TotalMinutes < 1)
        {
            return "Только что";
        }

        if (delta.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)delta.TotalMinutes)} мин. назад";
        }

        if (delta.TotalDays < 1)
        {
            return $"{Math.Max(1, (int)delta.TotalHours)} ч. назад";
        }

        return $"{Math.Max(1, (int)delta.TotalDays)} дн. назад";
    }
}

public sealed record RecentItemViewModel(string Title, string Kind, string UpdatedAtText);
