using AdaptiveRemote.Backend.ApiTests.Support;
using AdaptiveRemote.TestUtilities;
using Reqnroll;

namespace AdaptiveRemote.Backend.ApiTests.StepDefinitions;

[Binding]
public class AuthenticationSteps
{
    private readonly ServiceContext _context;
    private readonly TestClient _testClient;

    public AuthenticationSteps(ServiceContext context, TestClient testClient)
    {
        _context = context;
        _testClient = testClient;
    }

    [Given("the client has a valid Authorization token")]
    public void GivenClientHasValidAuthenticationToken()
    {
        _testClient.AuthorizationToken = _context.Fixture.CreateToken();
    }

    [Given("the client has a no Authorization token")]
    public void GivenClientHasNoAuthorizationToken()
    {
        _testClient.AuthorizationToken = string.Empty;
    }

    [Given("the client has an expired Authorization token")]
    public void GivenClientHasExpiredAuthorizationToken()
    {
        _testClient.AuthorizationToken = _context.Fixture.CreateExpiredToken();
    }
}
