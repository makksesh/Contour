/// <summary>
/// ViewModel вкладки «RAG Search» в ProjectWorkspaceView.
///
/// Отвечает за:
///   - Хранение текущего projectId (SetProject).
///   - SearchCommand → POST /api/rag/search → заполняет Results.
///   - Отображение состояний: IsLoading, IsEmpty, HasError.
///
/// Зависимости: RagService.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Rag;
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

    public bool IsEmpty => !IsLoading && Results.Count == 0;

    // ─── ctor ──────────────────────────────────────────────────────────────

    public RagSearchViewModel(RagService ragService)
        => _ragService = ragService;

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
        OnPropertyChanged(nameof(IsEmpty));

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
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    private bool CanSearch() => !string.IsNullOrWhiteSpace(Query);
}
