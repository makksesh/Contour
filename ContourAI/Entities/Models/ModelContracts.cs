using System;

namespace ContourAI.Entities.Models;

public sealed record ModelEndpointDto(
    Guid Id,
    string DisplayName,
    string ModelName,
    string BaseUrl,
    string ModelType,
    int ContextWindowTokens,
    bool IsEnabled,
    string? ApiKey = null);

public sealed record CreateModelEndpointRequest(
    string DisplayName,
    string ModelName,
    string BaseUrl,
    string ModelType,
    int ContextWindowTokens,
    string? ApiKey);

public sealed record UpdateModelEndpointRequest(
    string DisplayName,
    string ModelName,
    string BaseUrl,
    int ContextWindowTokens,
    string? ApiKey);

public sealed record SetModelEndpointEnabledRequest(bool IsEnabled);
