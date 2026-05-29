using System;

namespace ContourAI.Entities.Projects;

public sealed record ProjectSettingsDto(
    Guid?  ChatModelEndpointId,
    Guid?  EmbeddingModelEndpointId,
    string SystemPrompt,
    int    MaxTokens,
    float  Temperature,
    int    RagTopK,
    bool   UseRagContext,
    int    ContextWindowSize);
