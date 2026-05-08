using System.Net.Http.Headers;
using AdaptiveRemote.TestUtilities;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndToEndTests.TestServices;

public class TestClient
{
    private HttpClient _httpClient = new();

    public string AuthorizationToken { get; set; } = string.Empty;

    private static int NextClientID = 1;
    private readonly int _clientID = NextClientID++;
    private readonly ILogger<TestClient> _log;
    private int _requestCount = 0;

    public TestClient(ILoggerFactory loggerFactory)
    {
        _log = loggerFactory.CreateLogger<TestClient>();
        _log.LogInformation("Created Client {ClientID}\n{CallStack}", _clientID, new System.Diagnostics.StackTrace());
    }

    public HttpResponseMessage? SendRequest(HttpMethod method, Uri url, string? body = null)
    {
        int requestNumber = ++_requestCount;
        _log.LogInformation(
            """
            Client {ClientID} sending request #{RequestNumber}:
            {Method} {Url}
            {Body}
            """,
            requestNumber,
            _clientID,
            method.Method,
            url,
            body);

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

        HttpResponseMessage response = WaitHelpers.WaitForAsyncTask(ct => _httpClient.SendAsync(request, ct));

        _log.LogInformation(
            """
            Client {ClientID} received response for request #{RequestNumber}: 
            {StatusCode} {ResponsePhrase}
            {ResponseBody}
            """,
            _clientID,
            requestNumber,
            (int)response.StatusCode,
            response.ReasonPhrase,
            response.Content.ReadAsStringAsync().Result);

        return response;
    }

    public override string ToString() => $"Client {_clientID}";
}
