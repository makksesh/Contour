using System;

namespace ContourAI.Entities.Rag;

public sealed record RagChunkDto(
    Guid    ChunkId,
    Guid    DocumentId,
    string  Content,
    double  Score,
    string? FileName,
    string? FilePath,
    int?    LineStart,
    int?    LineEnd);
