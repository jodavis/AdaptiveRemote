using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AdaptiveRemote.TestUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdaptiveRemote.EndToEndTests.TestServices;

public class TestClient
{
    private HttpClient _httpClient = new();

    public string AuthorizationToken { get; set; } = string.Empty;

    private static int NextClientID = 1;
    private readonly int _clientID = NextClientID++;
    private readonly ILogger<TestClient> _log;
    private int _requestCount = 0;

    private HttpResponseMessage? _lastResponseMessage;
    private string? _lastResponseBody;
    private object? _lastParsedObject = null;

    public TestClient(ILoggerFactory loggerFactory)
    {
        _log = loggerFactory.CreateLogger<TestClient>();
    }

    public HttpResponseMessage LastResponse => _lastResponseMessage 
        ?? throw new AssertFailedException("No request has been sent yet.");
    public string LastResponseBody => _lastResponseBody
        ?? throw new AssertFailedException("No request has been sent yet.");
    public object LastResponseObject => _lastParsedObject 
        ?? throw new AssertFailedException("The response body has not been deserialized yet. Ensure that the step 'the response body represents a {JsonTypeInfo}' is called before this step.");

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

        _lastParsedObject = null;
        _lastResponseMessage = WaitHelpers.WaitForAsyncTask(ct => _httpClient.SendAsync(request, ct));
        _lastResponseBody = WaitHelpers.WaitForAsyncTask(LastResponse.Content.ReadAsStringAsync);

        _log.LogInformation(
            """
            Client {ClientID} received response for request #{RequestNumber}: 
            {StatusCode} {ResponsePhrase}
            {ResponseBody}
            """,
            _clientID,
            requestNumber,
            (int)LastResponse.StatusCode,
            LastResponse.ReasonPhrase,
            LastResponse.Content.ReadAsStringAsync().Result);

        return LastResponse;
    }

    public void ParseResponseAs(JsonTypeInfo jsonTypeInfo)
    {
        Assert.IsNotNull(LastResponseBody, "No response body to parse. Make sure to call SendRequest first and that the response has a body.");

        try
        {
            _lastParsedObject = JsonSerializer.Deserialize(LastResponseBody, jsonTypeInfo);
            Assert.IsNotNull(_lastParsedObject, "Deserialization returned null. Response body may be empty or not match the expected format.");
        }
        catch (JsonException ex)
        {
            Assert.Fail("Failed to parse the response body as JSON. {0}", ex.Message);
            throw;
        }
    }

    public override string ToString() => $"Client {_clientID}";
}
