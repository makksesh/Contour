/// <summary>
/// Запрос на обновление настроек проекта.
/// PATCH /api/projects/{projectId}/settings → 204 No Content.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;

namespace ContourAI.Entities.Projects;

public sealed record UpdateProjectSettingsRequest(
    Guid?  ChatModelEndpointId,
    Guid?  EmbeddingModelEndpointId,
    string SystemPrompt,
    int    MaxTokens,
    float  Temperature,
    int    RagTopK,
    bool   UseRagContext,
    int    ContextWindowSize = 10);
