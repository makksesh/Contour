/// <summary>
/// ViewModel вкладки «RAG Search» рабочего пространства проекта.
///
/// Сценарий:
/// 1. SetProject(projectId) — вызывается из ProjectWorkspaceViewModel при смене проекта.
/// 2. SearchCommand — POST /api/rag/search { projectId, query, topK }
///    → заполняет Results коллекцией RagChunkItemViewModel.
/// 3. ClearCommand — очищает Results и Query.
///
/// TopK (1–20) задаётся пользователем; по умолчанию 5.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Shared.Api;

namespace ContourAI.Features.Projects;

public sealed partial class RagSearchViewModel : ObservableObject
{
    private readonly RagService _ragService;
    private Guid _projectId;

    // ─── Коллекция результатов ────────────────────────────────────────────────

    public ObservableCollection<RagChunkItemViewModel> Results { get; } = new();

    // ─── Ввод ────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private string _query = string.Empty;

    [ObservableProperty] private int _topK = 5;

    // ─── Состояния ───────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private bool _isLoading;

    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool   _isEmpty;
    [ObservableProperty] private bool   _hasSearched;

    // ─── Дисплей-свойства ────────────────────────────────────────────────────

    public string ResultsSummary => Results.Count switch
    {
        0 => string.Empty,
        1 => "1 чанк найден",
        _ => $"{Results.Count} чанков найдено"
    };

    // ─── Конструктор ─────────────────────────────────────────────────────────

    public RagSearchViewModel(RagService ragService)
    {
        _ragService = ragService;
    }

    // ─── Инициализация ───────────────────────────────────────────────────────

    /// <summary>Вызывается из ProjectWorkspaceViewModel при смене проекта.</summary>
    public void SetProject(Guid projectId)
    {
        if (_projectId == projectId) return;
        _projectId = projectId;
        ClearCommand.Execute(null);
    }

    // ─── Команды ─────────────────────────────────────────────────────────────

    private bool CanSearch() =>
        !string.IsNullOrWhiteSpace(Query) && !IsLoading;

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync(CancellationToken ct = default)
    {
        IsLoading  = true;
        HasError   = false;
        HasSearched = true;
        Results.Clear();
        try
        {
            var topK   = Math.Clamp(TopK, 1, 20);
            var chunks = await _ragService.SearchAsync(_projectId, Query.Trim(), topK, ct);

            if (chunks is null) return;

            foreach (var c in chunks)
                Results.Add(new RagChunkItemViewModel(c));

            IsEmpty = Results.Count == 0;
            OnPropertyChanged(nameof(ResultsSummary));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void Clear()
    {
        Query   = string.Empty;
        Results.Clear();
        IsEmpty     = false;
        HasSearched = false;
        HasError    = false;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(ResultsSummary));
    }
}
