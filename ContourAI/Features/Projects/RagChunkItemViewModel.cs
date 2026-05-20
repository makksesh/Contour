/// <summary>
/// ViewModel одного чанка в списке результатов RAG-поиска.
///
/// Создаётся из RagChunkDto и предоставляет подготовленные для View свойства:
///   - LocationLabel  — «имяфайла : строка N» или просто имя файла
///   - ScoreLabel     — процент релевантности, например «87%»
///   - ContentPreview — первые 400 символов текста чанка
///   - IsExpanded     — показывать полный текст по кнопке «Развернуть»
///
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Rag;

namespace ContourAI.Features.Projects;

public sealed partial class RagChunkItemViewModel : ObservableObject
{
    // ─── Идентификаторы ─────────────────────────────────────────────────────

    public Guid   ChunkId    { get; }
    public Guid   DocumentId { get; }

    // ─── Отображаемые свойства ──────────────────────────────────────────────

    public string FileName      { get; }
    public string LocationLabel { get; }
    public string ScoreLabel    { get; }
    public string FullContent   { get; }
    public string ContentPreview { get; }
    public bool   HasMore        { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayContent))]
    [NotifyPropertyChangedFor(nameof(ExpandButtonLabel))]
    private bool _isExpanded;

    public string DisplayContent    => IsExpanded ? FullContent : ContentPreview;
    public string ExpandButtonLabel => IsExpanded ? "Свернуть" : "Развернуть";

    // ─── ctor ─────────────────────────────────────────────────────────────────

    public RagChunkItemViewModel(RagChunkDto dto)
    {
        ChunkId      = dto.ChunkId;
        DocumentId   = dto.DocumentId;
        FullContent  = dto.Content;
        ScoreLabel   = $"{(int)Math.Round(dto.Score * 100)}%";
        FileName     = dto.FileName ?? "Неизвестный файл";

        LocationLabel = dto.LineStart.HasValue
            ? $"{FileName} : строка {dto.LineStart}"
            : FileName;

        const int previewLength = 400;
        HasMore        = dto.Content.Length > previewLength;
        ContentPreview = HasMore ? dto.Content[..previewLength] + "…" : dto.Content;
    }

    // ─── Команды ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;
}
