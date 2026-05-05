using System.Collections.Concurrent;
using System.Net;
using System.Text;
using FluentAssertions;

namespace AdaptiveRemote.Services.CloudAssets;

[TestClass]
public class CognitoTokenProviderTests
{
    private const string TokenEndpointUrl = "https://cognito.example.com/oauth2/token";
    private const string ClientId = "test-client-id";
    private const string ClientSecret = "test-client-secret";

    private readonly MockLogger<CognitoTokenProvider> MockLogger = new();

    private static CloudSettings DefaultSettings => new()
    {
        ClientId = ClientId,
        ClientSecret = ClientSecret,
        CognitoTokenEndpointUrl = TokenEndpointUrl,
    };

    private CognitoTokenProvider MakeSut(
        CloudSettings? settings = null,
        HttpMessageHandler? httpMessageHandler = null) =>
        new(
            new MockOptions<CloudSettings>(settings ?? DefaultSettings),
            MockLogger,
            httpMessageHandler ?? new FakeHttpMessageHandler(
                BuildTokenResponse("default-token", expiresIn: 3600)));

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private static HttpResponseMessage BuildTokenResponse(string token, int expiresIn = 3600) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$$"""{"access_token":"{{{token}}}","expires_in":{{{expiresIn}}},"token_type":"Bearer"}""",
                Encoding.UTF8,
                "application/json"),
        };

    // ─────────────────────────────────────────────────────────────────
    // Tests
    // ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task CognitoTokenProvider_GetTokenAsync_FirstCall_FetchesAndReturnsTokenAsync()
    {
        // Arrange
        FakeHttpMessageHandler handler = new(BuildTokenResponse("first-token"));
        using CognitoTokenProvider sut = MakeSut(httpMessageHandler: handler);

        // Act
        string token = await sut.GetTokenAsync(CancellationToken.None);

        // Assert
        token.Should().Be("first-token");
        handler.CallCount.Should().Be(1);
        MockLogger.VerifyMessages(log =>
        {
            log.CognitoTokenProvider_AcquiringToken();
            log.CognitoTokenProvider_TokenAcquired();
        });
    }

    [TestMethod]
    public async Task CognitoTokenProvider_GetTokenAsync_SecondCallBeforeExpiry_ReturnsCachedTokenAsync()
    {
        // Arrange — token expires in 1 hour, well outside the 60-second buffer
        FakeHttpMessageHandler handler = new(BuildTokenResponse("cached-token", expiresIn: 3600));
        using CognitoTokenProvider sut = MakeSut(httpMessageHandler: handler);

        // Act — call twice
        string first = await sut.GetTokenAsync(CancellationToken.None);
        string second = await sut.GetTokenAsync(CancellationToken.None);

        // Assert — only one HTTP request made; cached token returned on second call
        first.Should().Be("cached-token");
        second.Should().Be("cached-token");
        handler.CallCount.Should().Be(1);
        MockLogger.VerifyMessages(log =>
        {
            log.CognitoTokenProvider_AcquiringToken();
            log.CognitoTokenProvider_TokenAcquired();
        });
    }

    [TestMethod]
    public async Task CognitoTokenProvider_GetTokenAsync_CallAfterNearExpiry_RefreshesTokenAsync()
    {
        // Arrange — first response expires in 30 seconds (within 60-second buffer → will refresh)
        FakeHttpMessageHandler handler = new(
            BuildTokenResponse("initial-token", expiresIn: 30),
            BuildTokenResponse("refreshed-token", expiresIn: 3600));
        using CognitoTokenProvider sut = MakeSut(httpMessageHandler: handler);

        // Act — first call acquires; second call finds token near expiry and refreshes
        string first = await sut.GetTokenAsync(CancellationToken.None);
        string second = await sut.GetTokenAsync(CancellationToken.None);

        // Assert
        first.Should().Be("initial-token");
        second.Should().Be("refreshed-token");
        handler.CallCount.Should().Be(2);
        MockLogger.VerifyMessages(log =>
        {
            log.CognitoTokenProvider_AcquiringToken();
            log.CognitoTokenProvider_TokenAcquired();
            log.CognitoTokenProvider_AcquiringToken();
            log.CognitoTokenProvider_TokenAcquired();
        });
    }

    [TestMethod]
    public async Task CognitoTokenProvider_GetTokenAsync_CancellationRequested_ThrowsOperationCanceledExceptionAsync()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        cts.Cancel();
        FakeHttpMessageHandler handler = new(BuildTokenResponse("token"));
        using CognitoTokenProvider sut = MakeSut(httpMessageHandler: handler);

        // Act
        Func<Task> act = async () => await sut.GetTokenAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.CallCount.Should().Be(0);
    }

    [TestMethod]
    public async Task CognitoTokenProvider_GetTokenAsync_HttpFailure_PropagatesExceptionAsync()
    {
        // Arrange
        FakeHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using CognitoTokenProvider sut = MakeSut(httpMessageHandler: handler);

        // Act
        Func<Task> act = async () => await sut.GetTokenAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        MockLogger.VerifyMessages(log =>
        {
            log.CognitoTokenProvider_AcquiringToken();
            log.CognitoTokenProvider_AcquireTokenFailed(
                new HttpRequestException("Response status code does not indicate success: 401 (Unauthorized)."));
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // FakeHttpMessageHandler — returns pre-configured responses in order
    // ─────────────────────────────────────────────────────────────────

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly ConcurrentQueue<HttpResponseMessage> _responses;
        private int _callCount;

        public int CallCount => _callCount;

        public FakeHttpMessageHandler(params HttpResponseMessage[] responses)
        {
            _responses = new ConcurrentQueue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int callNumber = Interlocked.Increment(ref _callCount);

            if (!_responses.TryDequeue(out HttpResponseMessage? response))
            {
                throw new InvalidOperationException(
                    $"FakeHttpMessageHandler: no response configured for call #{callNumber}");
            }

            return Task.FromResult(response);
        }
    }
}
