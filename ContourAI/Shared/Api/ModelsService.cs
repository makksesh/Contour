using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ContourAI.Entities.Models;

namespace ContourAI.Shared.Api;

public sealed class ModelsService
{
    private readonly AuthorizedHttpClientFactory _httpFactory;
    private readonly SessionAuthService _sessionAuthService;

    public ModelsService(
        AuthorizedHttpClientFactory httpFactory,
        SessionAuthService sessionAuthService)
    {
        _httpFactory = httpFactory;
        _sessionAuthService = sessionAuthService;
    }

    public async Task<IReadOnlyList<ModelEndpointDto>?> GetModelsAsync(
        string? modelType = null,
        CancellationToken ct = default)
    {
        var http = _httpFactory.CreateAuthorized();
        var path = string.IsNullOrWhiteSpace(modelType)
            ? "api/models"
            : $"api/models?modelType={Uri.EscapeDataString(modelType)}";

        using var response = await http.GetAsync(path, ct);
        if (HandleAuth(response.StatusCode))
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<ModelEndpointDto>>(cancellationToken: ct);
    }

    public async Task<ModelEndpointDto?> GetModelAsync(Guid endpointId, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateAuthorized();
        using var response = await http.GetAsync($"api/models/{endpointId}", ct);
        if (HandleAuth(response.StatusCode))
            return null;
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ModelEndpointDto>(cancellationToken: ct);
    }

    public async Task<ModelEndpointDto?> CreateAsync(CreateModelEndpointRequest request, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateAuthorized();
        using var response = await http.PostAsJsonAsync("api/models", request, ct);
        if (HandleAuth(response.StatusCode))
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ModelEndpointDto>(cancellationToken: ct);
    }

    public async Task<ModelEndpointDto?> UpdateAsync(Guid endpointId, UpdateModelEndpointRequest request, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateAuthorized();
        using var response = await http.PutAsJsonAsync($"api/models/{endpointId}", request, ct);
        if (HandleAuth(response.StatusCode))
            return null;
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ModelEndpointDto>(cancellationToken: ct);
    }

    public async Task<bool> SetEnabledAsync(Guid endpointId, bool isEnabled, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateAuthorized();
        using var response = await http.PatchAsJsonAsync(
            $"api/models/{endpointId}/enabled",
            new SetModelEndpointEnabledRequest(isEnabled),
            ct);

        if (HandleAuth(response.StatusCode))
            return false;
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid endpointId, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateAuthorized();
        using var response = await http.DeleteAsync($"api/models/{endpointId}", ct);
        if (HandleAuth(response.StatusCode))
            return false;
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    private bool HandleAuth(HttpStatusCode code)
    {
        if (code is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _sessionAuthService.HandleUnauthorized();
            return true;
        }

        return false;
    }
}
