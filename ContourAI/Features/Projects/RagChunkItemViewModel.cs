/// <summary>
/// ViewModel одного чанка в результатах RAG-поиска.
/// Хранит RagChunkDto + UI-состояние IsExpanded (раскрыть полный текст).
/// ScorePercent — Score [0..1] в процентах для отображения.
/// LocationLabel — «FileName : строка N» (или только FileName).
/// Preview — первые 300 символов Content; FullContent — весь текст.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Rag;

namespace ContourAI.Features.Projects;

public sealed partial class RagChunkItemViewModel : ObservableObject
{
    private const int PreviewLength = 300;

    // ─── Данные чанка ────────────────────────────────────────────────────────

    public Guid    ChunkId    { get; }
    public Guid    DocumentId { get; }
    public string  FileName   { get; }
    public string  FullContent { get; }
    public int     ScorePercent { get; }

    // ─── UI-состояние ────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpandButtonLabel))]
    private bool _isExpanded;

    // ─── Дисплей-свойства ────────────────────────────────────────────────────

    public string LocationLabel { get; }

    public string Preview => FullContent.Length <= PreviewLength
        ? FullContent
        : FullContent[..PreviewLength] + "…";

    public bool HasMore => FullContent.Length > PreviewLength;

    public string ExpandButtonLabel => IsExpanded ? "Свернуть" : "Развернуть";

    // ─── Команды ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;

    // ─── Конструктор ─────────────────────────────────────────────────────────

    public RagChunkItemViewModel(RagChunkDto dto)
    {
        ChunkId      = dto.ChunkId;
        DocumentId   = dto.DocumentId;
        FullContent  = dto.Content;
        ScorePercent = (int)Math.Round(dto.Score * 100);
        FileName     = dto.FileName ?? dto.FilePath ?? "Неизвестный файл";

        LocationLabel = dto.LineStart.HasValue
            ? $"{FileName} : строка {dto.LineStart}"
            : FileName;
    }
}
