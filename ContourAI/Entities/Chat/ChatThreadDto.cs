namespace ContourAI.Entities.Chat;

/// <summary>DTO треда (проектного или глобального).</summary>
public sealed record ChatThreadDto(
    Guid      Id,
    Guid?     ProjectId,
    string    Title,
    int       MessageCount,
    DateTime? LastMessageAtUtc,
    DateTime  CreatedAtUtc)
{
    /// <summary>true — глобальный тред (не привязан к проекту).</summary>
    public bool IsGlobal => ProjectId is null;
}
