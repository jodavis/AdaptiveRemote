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

    private static HttpResponseMessage BuildTokenResponse(string token, int expiresIn = 3600) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$$"""{"access_token":"{{{token}}}","expires_in":{{{expiresIn}}},"token_type":"Bearer"}""",
                Encoding.UTF8,
                "application/json"),
        };

    [TestMethod]
    public void CognitoTokenProvider_GetTokenAsync_FirstCall_FetchesAndReturnsToken()
    {
        // Arrange
        FakeHttpMessageHandler handler = new(BuildTokenResponse("first-token"));
        using CognitoTokenProvider sut = MakeSut(httpMessageHandler: handler);

        // Act
        Task<string?> tokenTask = sut.GetTokenAsync(CancellationToken.None);

        // Assert
        tokenTask.Should().BeComplete();
        tokenTask.Result.Should().Be("first-token");
        handler.CallCount.Should().Be(1);
        VerifyTokenRequest(handler, TokenEndpointUrl, ClientId, ClientSecret);
        MockLogger.VerifyMessages(log =>
        {
            log.CognitoTokenProvider_AcquiringToken();
            log.CognitoTokenProvider_TokenAcquired();
        });
    }

    [TestMethod]
    public void CognitoTokenProvider_GetTokenAsync_SecondCallBeforeExpiry_ReturnsCachedToken()
    {
        // Arrange
        FakeHttpMessageHandler handler = new(BuildTokenResponse("cached-token", expiresIn: 3600));
        using CognitoTokenProvider sut = MakeSut(httpMessageHandler: handler);

        // Act
        Task<string?> firstTask = sut.GetTokenAsync(CancellationToken.None);
        Task<string?> secondTask = sut.GetTokenAsync(CancellationToken.None);

        // Assert
        firstTask.Should().BeComplete();
        secondTask.Should().BeComplete();
        firstTask.Result.Should().Be("cached-token");
        secondTask.Result.Should().Be("cached-token");
        handler.CallCount.Should().Be(1);
    }

    [TestMethod]
    public void CognitoTokenProvider_GetTokenAsync_CallAfterNearExpiry_RefreshesToken()
    {
        // Arrange
        FakeHttpMessageHandler handler = new(
            BuildTokenResponse("initial-token", expiresIn: 30),
            BuildTokenResponse("refreshed-token", expiresIn: 3600));
        using CognitoTokenProvider sut = MakeSut(httpMessageHandler: handler);

        // Act
        Task<string?> firstTask = sut.GetTokenAsync(CancellationToken.None);
        Task<string?> secondTask = sut.GetTokenAsync(CancellationToken.None);

        // Assert
        firstTask.Should().BeComplete();
        secondTask.Should().BeComplete();
        firstTask.Result.Should().Be("initial-token");
        secondTask.Result.Should().Be("refreshed-token");
        handler.CallCount.Should().Be(2);
    }

    [TestMethod]
    public void CognitoTokenProvider_GetTokenAsync_CancellationRequestedBeforeCall_CancelsWithoutHttpCall()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        cts.Cancel();
        FakeHttpMessageHandler handler = new(BuildTokenResponse("token"));
        using CognitoTokenProvider sut = MakeSut(httpMessageHandler: handler);

        // Act
        Task<string?> tokenTask = sut.GetTokenAsync(cts.Token);

        // Assert
        tokenTask.Should().BeCanceled();
        handler.CallCount.Should().Be(0);
    }

    [TestMethod]
    public void CognitoTokenProvider_GetTokenAsync_CancellationWhileWaitingForLock_EndsWithOperationCanceledOutcome()
    {
        // Arrange
        TaskCompletionSource firstRequestStarted = new();
        TaskCompletionSource<HttpResponseMessage> firstResponse = new();
        FakeHttpMessageHandler handler = new(
            (_, ct) =>
            {
                firstRequestStarted.TrySetResult();
                return firstResponse.Task.WaitAsync(ct);
            },
            (_, _) => Task.FromResult(BuildTokenResponse("second-token")));
        using CognitoTokenProvider sut = MakeSut(httpMessageHandler: handler);

        Task<string?> firstTokenTask = sut.GetTokenAsync(CancellationToken.None);
        WaitForCondition(() => firstRequestStarted.Task.IsCompleted, TimeSpan.FromSeconds(5))
            .Should().BeTrue(because: "the first request must acquire the lock before the second call is cancelled");

        using CancellationTokenSource secondCallCancellation = new();
        Task<string?> secondTokenTask = sut.GetTokenAsync(secondCallCancellation.Token);

        // Act
        secondCallCancellation.Cancel();

        // Assert
        firstTokenTask.Should().NotBeComplete();
        WaitForCondition(() => secondTokenTask.IsCompleted, TimeSpan.FromSeconds(5))
            .Should().BeTrue();
        AssertCanceledOutcome(secondTokenTask);
        handler.CallCount.Should().Be(1);

        firstResponse.SetResult(BuildTokenResponse("first-token"));
        firstTokenTask.Should().BeComplete();
        firstTokenTask.Result.Should().Be("first-token");
    }

    [TestMethod]
    public void CognitoTokenProvider_GetTokenAsync_HttpFailure_PropagatesException()
    {
        // Arrange
        FakeHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using CognitoTokenProvider sut = MakeSut(httpMessageHandler: handler);

        // Act
        Task<string?> tokenTask = sut.GetTokenAsync(CancellationToken.None);

        // Assert
        tokenTask.Should().BeFaultedWith(new HttpRequestException("Response status code does not indicate success: 401 (Unauthorized)."));
        MockLogger.VerifyMessages(log =>
        {
            log.CognitoTokenProvider_AcquiringToken();
            log.CognitoTokenProvider_AcquireTokenFailed(
                new HttpRequestException("Response status code does not indicate success: 401 (Unauthorized)."));
        });
    }

    [TestMethod]
    public void CognitoTokenProvider_GetTokenAsync_MissingCredentials_ReturnsNullWithoutCallingHttp()
    {
        // Arrange
        CloudSettings settings = new()
        {
            ClientId = string.Empty,
            ClientSecret = string.Empty,
            CognitoTokenEndpointUrl = TokenEndpointUrl,
        };
        FakeHttpMessageHandler handler = new(BuildTokenResponse("token"));
        using CognitoTokenProvider sut = MakeSut(settings, handler);

        // Act
        Task<string?> tokenTask = sut.GetTokenAsync(CancellationToken.None);

        // Assert
        tokenTask.Should().BeComplete();
        tokenTask.Result.Should().BeNull();
        handler.CallCount.Should().Be(0);
        MockLogger.VerifyMessages(log => log.CognitoTokenProvider_MissingConfiguration());
    }

    [TestMethod]
    public void CognitoTokenProvider_GetTokenAsync_MissingEndpoint_ReturnsNullWithoutCallingHttp()
    {
        // Arrange
        CloudSettings settings = new()
        {
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            CognitoTokenEndpointUrl = string.Empty,
        };
        FakeHttpMessageHandler handler = new(BuildTokenResponse("token"));
        using CognitoTokenProvider sut = MakeSut(settings, handler);

        // Act
        Task<string?> tokenTask = sut.GetTokenAsync(CancellationToken.None);

        // Assert
        tokenTask.Should().BeComplete();
        tokenTask.Result.Should().BeNull();
        handler.CallCount.Should().Be(0);
    }

    private static void VerifyTokenRequest(
        FakeHttpMessageHandler handler,
        string expectedUrl,
        string expectedClientId,
        string expectedClientSecret)
    {
        handler.Requests.Should().ContainSingle();
        CapturedHttpRequest request = handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Post.Method);
        request.Url.Should().Be(expectedUrl);

        request.Body.Should().NotBeNull();
        Dictionary<string, string> formValues = ParseFormUrlEncoded(request.Body!);
        formValues["grant_type"].Should().Be("client_credentials");
        formValues["client_id"].Should().Be(expectedClientId);
        formValues["client_secret"].Should().Be(expectedClientSecret);
    }

    private static Dictionary<string, string> ParseFormUrlEncoded(string body) =>
        body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                static parts => WebUtility.UrlDecode(parts[0]),
                static parts => parts.Length > 1 ? WebUtility.UrlDecode(parts[1]) : string.Empty);

    private static void AssertCanceledOutcome(Task task)
    {
        bool wasCanceled = task.IsCanceled
            || (task.IsFaulted && task.Exception?.InnerException is OperationCanceledException);
        wasCanceled.Should().BeTrue("the operation should observe CancellationToken cancellation");
    }

    private static bool WaitForCondition(Func<bool> condition, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return condition();
    }
}
