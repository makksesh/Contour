/// <summary>
/// Настройки проекта, возвращаемые GET /api/projects/{id}/settings.
/// Все поля соответствуют телу PATCH /api/projects/{id}/settings.
/// Проект: DevAssistant / ContourAI.
/// </summary>

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
