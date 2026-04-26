using AdaptiveRemote.Contracts;
using System.Text.Json;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Services;

/// <summary>
/// HTTP client implementation of IRawLayoutStatusWriter.
/// Calls RawLayoutService over HTTP to write back validation results.
/// </summary>
public class HttpRawLayoutStatusWriter : IRawLayoutStatusWriter
{
    private readonly HttpClient _httpClient;

    public HttpRawLayoutStatusWriter(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task UpdateValidationResultAsync(Guid rawLayoutId, ValidationResult result, CancellationToken ct)
    {
        string json = JsonSerializer.Serialize(result, LayoutContractsJsonContext.Default.ValidationResult);
        StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _httpClient
            .PatchAsync($"/layouts/raw/{rawLayoutId}/validation-result", content, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}
