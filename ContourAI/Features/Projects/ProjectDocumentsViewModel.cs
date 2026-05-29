using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Shared.Api;

namespace ContourAI.Features.Projects;

public sealed partial class ProjectDocumentsViewModel : ObservableObject
{
    private readonly DocumentsService _documentsService;
    private readonly IndexingService  _indexingService;
    private Guid                      _projectId;

    // ─── Коллекция ────────────────────────────────────────────────────────────────

    public ObservableCollection<DocumentItemViewModel> Documents { get; } = new();

    // ─── Состояния ────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _isUploading;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool   _isEmpty;

    // ─── Событие для View (выбор файла через OpenFileDialog) ──────────────────

    /// <summary>
    /// View подписывается и возвращает выбранный путь к файлу
    /// (null = пользователь отменил диалог).
    /// </summary>
    public Func<Task<string?>>? PickFileAsync { get; set; }

    public ProjectDocumentsViewModel(
        DocumentsService documentsService,
        IndexingService  indexingService)
    {
        _documentsService = documentsService;
        _indexingService  = indexingService;
    }

    // ─── Инициализация ──────────────────────────────────────────────────────────────

    /// <summary>Вызывается из ProjectWorkspaceViewModel при смене проекта.</summary>
    public async Task LoadAsync(Guid projectId, CancellationToken ct = default)
    {
        _projectId = projectId;
        Documents.Clear();
        HasError = false;
        await RefreshAsync(ct);
    }

    // ─── Refresh ────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        HasError  = false;
        try
        {
            var docs = await _documentsService.GetProjectDocumentsAsync(_projectId, ct);
            if (docs == null) { HasError = true; ErrorMessage = "Не удалось загрузить документы."; return; }

            Documents.Clear();
            foreach (var dto in docs)
            {
                var item = new DocumentItemViewModel(dto);
                // Получаем статус индексирования для каждого документа
                var task = await _indexingService.GetStatusAsync(dto.Id, ct);
                item.ApplyTask(task);
                Documents.Add(item);
            }
            IsEmpty = Documents.Count == 0;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    // ─── Upload ───────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task UploadAsync()
    {
        if (PickFileAsync == null) return;
        var path = await PickFileAsync();
        if (string.IsNullOrEmpty(path)) return;

        IsUploading = true;
        HasError    = false;
        try
        {
            // 1. Загрузить файл
            var docDto = await _documentsService.UploadDocumentAsync(_projectId, path);
            if (docDto == null)
            {
                HasError     = true;
                ErrorMessage = "Не удалось загрузить файл.";
                return;
            }

            // 2. Сразу поставить в очередь на индексирование
            var taskDto = await _indexingService.QueueAsync(docDto.Id);

            var item = new DocumentItemViewModel(docDto);
            item.ApplyTask(taskDto);
            Documents.Insert(0, item);
            IsEmpty = false;
        }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { IsUploading = false; }
    }

    // ─── Delete ──────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteAsync(DocumentItemViewModel item)
    {
        item.IsDeleting = true;
        HasError        = false;
        try
        {
            var ok = await _documentsService.DeleteDocumentAsync(item.Id);
            if (!ok) { HasError = true; ErrorMessage = "Не удалось удалить документ."; return; }
            Documents.Remove(item);
            IsEmpty = Documents.Count == 0;
        }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { item.IsDeleting = false; }
    }

    // ─── Requeue ────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RequeueAsync(DocumentItemViewModel item)
    {
        if (item.TaskId == null) return;
        HasError = false;
        try
        {
            var task = await _indexingService.RequeueAsync(item.TaskId.Value);
            item.ApplyTask(task);
        }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
    }
}
