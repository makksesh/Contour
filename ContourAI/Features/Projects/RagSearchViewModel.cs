using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Shared.Api;

namespace ContourAI.Features.Projects;

public sealed partial class RagSearchViewModel : ObservableObject
{
    private readonly RagService _ragService;
    private Guid                _projectId;
    private CancellationTokenSource _cts = new();

    // ─── Ввод ─────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private string _query = string.Empty;

    [ObservableProperty] private int _topK = 5;

    // ─── Результаты ───────────────────────────────────────────────────────────

    public ObservableCollection<RagChunkItemViewModel> Results { get; } = [];

    // ─── Состояния ────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    /// <summary>
    /// True когда не идёт загрузка и список пуст.
    /// Триггерится и по IsLoading, и по CollectionChanged у Results.
    /// </summary>
    public bool IsEmpty => !IsLoading && Results.Count == 0;

    // ─── ctor ─────────────────────────────────────────────────────────────────

    public RagSearchViewModel(RagService ragService)
    {
        _ragService = ragService;
        Results.CollectionChanged += OnResultsChanged;
    }

    private void OnResultsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => OnPropertyChanged(nameof(IsEmpty));

    // ─── Инициализация ────────────────────────────────────────────────────────

    /// <summary>Вызывается из ProjectWorkspaceViewModel при открытии проекта.</summary>
    public void SetProject(Guid projectId)
    {
        if (_projectId == projectId) return;
        _projectId = projectId;
        Results.Clear();
        Query        = string.Empty;
        HasError     = false;
        ErrorMessage = string.Empty;
    }

    // ─── Команды ──────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();

        IsLoading = true;
        HasError  = false;
        Results.Clear();

        try
        {
            var chunks = await _ragService.SearchAsync(_projectId, Query, TopK, _cts.Token);
            if (chunks != null)
                foreach (var c in chunks)
                    Results.Add(new RagChunkItemViewModel(c));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanSearch() => !string.IsNullOrWhiteSpace(Query);
}
