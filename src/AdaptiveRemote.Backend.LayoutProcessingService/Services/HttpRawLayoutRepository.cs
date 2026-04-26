using AdaptiveRemote.Contracts;
using System.Text.Json;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Services;

/// <summary>
/// HTTP client implementation of IRawLayoutRepository.
/// Calls RawLayoutService over HTTP to fetch and manage raw layouts.
/// </summary>
public class HttpRawLayoutRepository : IRawLayoutRepository
{
    private readonly HttpClient _httpClient;

    public HttpRawLayoutRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<RawLayout?> GetAsync(Guid id, CancellationToken ct)
    {
        HttpResponseMessage response = await _httpClient
            .GetAsync($"/layouts/raw/{id}", ct)
            .ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, LayoutContractsJsonContext.Default.RawLayout);
    }

    public async Task<IReadOnlyList<RawLayout>> ListByUserAsync(string userId, CancellationToken ct)
    {
        HttpResponseMessage response = await _httpClient
            .GetAsync("/layouts/raw", ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, LayoutContractsJsonContext.Default.IReadOnlyListRawLayout)
            ?? Array.Empty<RawLayout>();
    }

    public async Task<RawLayout> SaveAsync(RawLayout layout, CancellationToken ct)
    {
        string json = JsonSerializer.Serialize(layout, LayoutContractsJsonContext.Default.RawLayout);
        StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _httpClient
            .PostAsync("/layouts/raw", content, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        string responseJson = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(responseJson, LayoutContractsJsonContext.Default.RawLayout)
            ?? throw new InvalidOperationException("SaveAsync returned null from RawLayoutService");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        HttpResponseMessage response = await _httpClient
            .DeleteAsync($"/layouts/raw/{id}", ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}
