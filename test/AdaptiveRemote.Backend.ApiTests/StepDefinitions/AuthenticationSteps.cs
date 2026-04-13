using System.Net;
using AdaptiveRemote.Backend.ApiTests.Support;
using FluentAssertions;
using Reqnroll;

namespace AdaptiveRemote.Backend.ApiTests.StepDefinitions;

[Binding]
public class AuthenticationSteps : IDisposable
{
    private readonly ServiceContext _context;

    public AuthenticationSteps(ServiceContext context)
    {
        _context = context;
    }

    [When(@"a test client with no Authorization header calls GET (.*)")]
    public async Task WhenAnonymousClientCallsGet(string endpoint)
    {
        using HttpClient client = _context.Fixture.CreateAnonymousHttpClient();
        _context.LastResponse = await client.GetAsync(endpoint);
        _context.LastResponseBody = await _context.LastResponse.Content.ReadAsStringAsync();
    }

    [When(@"a test client with a valid JWT calls GET (.*)")]
    public async Task WhenAuthenticatedClientCallsGet(string endpoint)
    {
        string token = _context.Fixture.CreateToken();
        using HttpClient client = _context.Fixture.CreateBearerHttpClient(token);
        _context.LastResponse = await client.GetAsync(endpoint);
        _context.LastResponseBody = await _context.LastResponse.Content.ReadAsStringAsync();
    }

    [When(@"a test client with an expired JWT calls GET (.*)")]
    public async Task WhenExpiredJwtClientCallsGet(string endpoint)
    {
        string token = _context.Fixture.CreateExpiredToken();
        using HttpClient client = _context.Fixture.CreateBearerHttpClient(token);
        _context.LastResponse = await client.GetAsync(endpoint);
        _context.LastResponseBody = await _context.LastResponse.Content.ReadAsStringAsync();
    }

    [Then(@"the response is (\d+) Unauthorized")]
    public void ThenTheResponseIsUnauthorized(int statusCode)
    {
        _context.LastResponse.Should().NotBeNull();
        ((int)_context.LastResponse!.StatusCode).Should().Be(statusCode);
        _context.LastResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public void Dispose()
    {
        // ServiceContext owns LastResponse and Fixture; nothing to dispose here.
        GC.SuppressFinalize(this);
    }
}
