using System.Text;
using System.Text.Json;
using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Services;

/// <summary>
/// HTTP client implementation of ILayoutCompilerClient.
/// Calls LayoutCompilerService over HTTP to compile raw layouts and generate previews.
/// </summary>
public class HttpLayoutCompilerClient : ILayoutCompilerClient
{
    private readonly HttpClient _httpClient;

    public HttpLayoutCompilerClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CompiledLayout> CompileAsync(RawLayout raw, CancellationToken ct)
    {
        string json = JsonSerializer.Serialize(raw, LayoutContractsJsonContext.Default.RawLayout);
        StringContent content = new(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _httpClient
            .PostAsync("/compile", content, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        string responseJson = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(responseJson, LayoutContractsJsonContext.Default.CompiledLayout)
            ?? throw new InvalidOperationException("CompileAsync returned null from LayoutCompilerService");
    }

    public async Task<PreviewLayout> CompilePreviewAsync(IReadOnlyList<RawLayoutElementDto> elements, CancellationToken ct)
    {
        string json = JsonSerializer.Serialize(elements, LayoutContractsJsonContext.Default.IReadOnlyListRawLayoutElementDto);
        StringContent content = new(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _httpClient
            .PostAsync("/compile/preview", content, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        string responseJson = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(responseJson, LayoutContractsJsonContext.Default.PreviewLayout)
            ?? throw new InvalidOperationException("CompilePreviewAsync returned null from LayoutCompilerService");
    }
}
