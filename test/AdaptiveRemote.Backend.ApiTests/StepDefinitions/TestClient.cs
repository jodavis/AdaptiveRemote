using System.Net.Http.Headers;
using AdaptiveRemote.TestUtilities;

namespace AdaptiveRemote.Backend.ApiTests.StepDefinitions;

public class TestClient
{
    private HttpClient _httpClient = new();

    public string AuthorizationToken { get; internal set; } = string.Empty;

    internal HttpResponseMessage? SendRequest(HttpMethod method, Uri url, string? body = null)
    {
        HttpRequestMessage request = new(method, url);

        if (!string.IsNullOrEmpty(body))
        {
            request.Content = new StringContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        if (!string.IsNullOrEmpty(AuthorizationToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthorizationToken);
        }

        return WaitHelpers.WaitForAsyncTask(ct => _httpClient.SendAsync(request, ct));
    }
}
