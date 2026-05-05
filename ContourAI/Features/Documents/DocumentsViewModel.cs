/// <summary>
/// ViewModel экрана документов проекта.
/// Загружает список, загружает файл через диалог выбора, удаляет, фильтрует по статусу.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Documents;
using ContourAI.Shared.Api;
using ContourAI.Shared.State;

namespace ContourAI.Features.Documents;

public sealed partial class DocumentsViewModel : ObservableObject
{
    private readonly DocumentsService    _documentsService;
    private readonly ProjectContextStore _projectContext;

    public ObservableCollection<DocumentCardViewModel> Documents { get; } = new();

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _isEmpty;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool   _isUploading;
    [ObservableProperty] private string _projectName  = string.Empty;

    // ─── Фильтр ────────────────────────────────────────────────────────────────
    [ObservableProperty] private int _selectedFilterIndex; // 0=All,1=Indexed,2=Pending,3=Processing,4=Failed

    partial void OnSelectedFilterIndexChanged(int value) => ApplyFilter();

    // ─── Подтверждение удаления ───────────────────────────────────────────────
    [ObservableProperty] private bool   _isDeleteConfirmOpen;
    [ObservableProperty] private string _deleteConfirmName = string.Empty;
    private Guid _pendingDeleteId;

    private List<DocumentCardViewModel> _allDocuments = new();

    public DocumentsViewModel(DocumentsService documentsService, ProjectContextStore projectContext)
    {
        _documentsService = documentsService;
        _projectContext   = projectContext;
        ProjectName       = projectContext.SelectedProjectName ?? "Project";
    }

    public async Task InitializeAsync()
    {
        ProjectName = _projectContext.SelectedProjectName ?? "Project";
        await LoadAsync();
    }

    // ─── Load ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        var projectId = _projectContext.SelectedProjectId;
        if (projectId == Guid.Empty) return;

        IsLoading    = true;
        HasError     = false;
        ErrorMessage = string.Empty;
        Documents.Clear();
        _allDocuments.Clear();

        try
        {
            var list = await _documentsService.GetProjectDocumentsAsync(projectId);
            if (list == null) return;

            foreach (var dto in list)
            {
                var card = new DocumentCardViewModel(dto);
                card.DeleteRequested += OnRequestDelete;
                _allDocuments.Add(card);
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = ex.Message;
        }
        finally { IsLoading = false; }
    }

    // ─── Filter ───────────────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        Documents.Clear();
        var filtered = SelectedFilterIndex switch
        {
            1 => _allDocuments.Where(d => d.Status == DocumentStatus.Indexed),
            2 => _allDocuments.Where(d => d.Status == DocumentStatus.Pending),
            3 => _allDocuments.Where(d => d.Status == DocumentStatus.Processing),
            4 => _allDocuments.Where(d => d.Status == DocumentStatus.Failed),
            _ => _allDocuments.AsEnumerable()
        };
        foreach (var card in filtered)
            Documents.Add(card);
        IsEmpty = Documents.Count == 0;
    }

    // ─── Upload ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Открывает нативный диалог выбора файла Avalonia и загружает выбранный файл.
    /// Принимает TopLevel для доступа к StorageProvider.
    /// </summary>
    [RelayCommand]
    private async Task UploadAsync(TopLevel? topLevel)
    {
        if (topLevel == null) return;
        var projectId = _projectContext.SelectedProjectId;
        if (projectId == Guid.Empty) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title         = "Select document to upload",
            AllowMultiple = false
        });

        if (files.Count == 0) return;

        var localPath = files[0].TryGetLocalPath();
        if (localPath == null) return;

        IsUploading  = true;
        HasError     = false;
        try
        {
            var dto = await _documentsService.UploadDocumentAsync(projectId, localPath);
            if (dto == null)
            {
                ErrorMessage = "Upload failed. Please try again.";
                HasError     = true;
                return;
            }
            var card = new DocumentCardViewModel(dto);
            card.DeleteRequested += OnRequestDelete;
            _allDocuments.Insert(0, card);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError     = true;
        }
        finally { IsUploading = false; }
    }

    // ─── Delete ───────────────────────────────────────────────────────────────

    private void OnRequestDelete(DocumentCardViewModel card)
    {
        _pendingDeleteId   = card.Id;
        DeleteConfirmName  = card.FileName;
        IsDeleteConfirmOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        IsDeleteConfirmOpen = false;
        try
        {
            var ok = await _documentsService.DeleteDocumentAsync(_pendingDeleteId);
            if (!ok) return;
            _allDocuments.RemoveAll(d => d.Id == _pendingDeleteId);
            ApplyFilter();
        }
        catch { /* TODO: show error */ }
    }

    [RelayCommand]
    private void CancelDelete() => IsDeleteConfirmOpen = false;
}
