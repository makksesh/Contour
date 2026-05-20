/// <summary>
/// DTO одного чанка, возвращаемого RAG-поиском.
/// POST /api/rag/search → RagChunkDto[].
/// Score — косинусное сходство [0..1]; LineStart — первая строка чанка в исходном файле.
/// Проект: DevAssistant / ContourAI.
/// </summary>

namespace ContourAI.Entities.Rag;

public sealed record RagChunkDto(
    Guid    ChunkId,
    Guid    DocumentId,
    string  Content,
    double  Score,
    string? FileName,
    string? FilePath,
    int?    LineStart);
