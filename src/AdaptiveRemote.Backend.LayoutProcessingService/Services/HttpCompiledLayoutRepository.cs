using AdaptiveRemote.Backend.LayoutProcessingService.Configuration;
using AdaptiveRemote.Contracts;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Services;

/// <summary>
/// HTTP client implementation of ICompiledLayoutRepository.
/// Calls CompiledLayoutService over HTTP to store and retrieve compiled layouts.
/// </summary>
public class HttpCompiledLayoutRepository : ICompiledLayoutRepository
{
    private readonly HttpClient _httpClient;

    public HttpCompiledLayoutRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CompiledLayout?> GetActiveForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        // TODO (ADR-171 / CompiledLayoutService DynamoDB task): Pass userId as a query parameter
        // (e.g. ?userId={userId}) once the real DynamoDB backend lands in CompiledLayoutService.
        // The current stub ignores userId, so this is harmless now, but will silently return
        // wrong-user data if this TODO is not addressed before real storage is wired up.
        HttpResponseMessage response = await _httpClient
            .GetAsync("/layouts/compiled/active", cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, LayoutContractsJsonContext.Default.CompiledLayout);
    }

    public async Task<IReadOnlyList<CompiledLayout>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        // TODO (ADR-171 / CompiledLayoutService DynamoDB task): Pass userId as a query parameter
        // (e.g. ?userId={userId}) once the real DynamoDB backend lands in CompiledLayoutService.
        // The current stub ignores userId, so this is harmless now, but will silently return
        // wrong-user data if this TODO is not addressed before real storage is wired up.
        HttpResponseMessage response = await _httpClient
            .GetAsync("/layouts/compiled", cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, LayoutContractsJsonContext.Default.IReadOnlyListCompiledLayout)
            ?? Array.Empty<CompiledLayout>();
    }

    public async Task<CompiledLayout?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient
            .GetAsync($"/layouts/compiled/{id}", cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, LayoutContractsJsonContext.Default.CompiledLayout);
    }

    public async Task<CompiledLayout> SaveAsync(CompiledLayout layout, CancellationToken cancellationToken = default)
    {
        string json = JsonSerializer.Serialize(layout, LayoutContractsJsonContext.Default.CompiledLayout);
        StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _httpClient
            .PostAsync("/layouts/compiled", content, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        string responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(responseJson, LayoutContractsJsonContext.Default.CompiledLayout)
            ?? throw new InvalidOperationException("SaveAsync returned null from CompiledLayoutService");
    }

    public async Task SetActiveAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient
            .PutAsync($"/layouts/compiled/{id}/active", content: null, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}
