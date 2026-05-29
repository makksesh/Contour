using System;

namespace ContourAI.Entities.Documents;

public sealed record DocumentDto(
    Guid           Id,
    Guid           ProjectId,
    string         FileName,
    string         OriginalPath,
    string?        ContentType,
    long           SizeBytes,
    DocumentStatus Status,
    string?        ErrorMessage,
    DateTime?      IndexedAtUtc,
    DateTime       CreatedAtUtc,
    int            ChunkCount);
