/// <summary>
/// ViewModel карточки документа.
/// Отображает имя, тип, размер, статус, ошибку (если есть).
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Documents;

namespace ContourAI.Features.Documents;

public sealed partial class DocumentCardViewModel : ObservableObject
{
    public Guid           Id          { get; }
    public string         FileName    { get; }
    public string         ContentType { get; }
    public long           SizeBytes   { get; }
    public DateTime       CreatedAt   { get; }
    public string?        ErrorMessage { get; }

    [ObservableProperty] private DocumentStatus _status;

    // ─── computed labels ──────────────────────────────────────────────────────────

    public string SizeLabel => SizeBytes switch
    {
        < 1024               => $"{SizeBytes} B",
        < 1024 * 1024        => $"{SizeBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{SizeBytes / (1024.0 * 1024):F1} MB",
        _                    => $"{SizeBytes / (1024.0 * 1024 * 1024):F1} GB"
    };

    public string StatusLabel => Status switch
    {
        DocumentStatus.Uploaded   => "• Загружен",
        DocumentStatus.Pending    => "⏳ В очереди",
        DocumentStatus.Processing => "↻ Обрабатывается",
        DocumentStatus.Indexed    => "✓ Индексирован",
        DocumentStatus.Failed     => "✗ Ошибка",
        _                         => Status.ToString()
    };

    public string StatusColor => Status switch
    {
        DocumentStatus.Indexed    => "#8D9E73",
        DocumentStatus.Failed     => "#8C4E49",
        DocumentStatus.Processing => "#B88A56",
        DocumentStatus.Pending    => "#8F8477",
        _                         => "#8F8477"
    };

    public bool HasError => Status == DocumentStatus.Failed && !string.IsNullOrEmpty(ErrorMessage);

    // ─── events ─────────────────────────────────────────────────────────────────

    public event Action<DocumentCardViewModel>? DeleteRequested;

    public DocumentCardViewModel(DocumentDto dto)
    {
        Id           = dto.Id;
        FileName     = dto.FileName;
        ContentType  = dto.ContentType ?? "неизвестно";
        SizeBytes    = dto.SizeBytes;
        CreatedAt    = dto.CreatedAtUtc;
        ErrorMessage = dto.ErrorMessage;
        _status      = dto.Status;
    }

    [RelayCommand]
    private void Delete() => DeleteRequested?.Invoke(this);
}
