using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;
using AdaptiveRemote.EndToEndTests.TestServices;
using AdaptiveRemote.EndToEndTests.TestServices.Backend;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Backend;

[Binding]
public class AuthenticationSteps
{
    private readonly ISimulatedEnvironment _environment;
    private readonly TestClient _testClient;

    // Use a unique user ID per fixture so each scenario operates on isolated data
    // even when DynamoDB is shared across test scenarios via the shared LocalStack.
    private readonly string _testUserId = $"test-user-{Guid.NewGuid():N}";

    public AuthenticationSteps(ISimulatedEnvironment environment, TestClient testClient)
    {
        _environment = environment;
        _testClient = testClient;
    }

    [Given("the client has a valid Authorization token")]
    public void GivenClientHasValidAuthenticationToken()
    {
        _testClient.AuthorizationToken = _environment.JwtAuthority.CreateToken(_testUserId);
    }

    [Given("the client has no Authorization token")]
    public void GivenClientHasNoAuthorizationToken()
    {
        _testClient.AuthorizationToken = string.Empty;
    }

    [Given("the client has an expired Authorization token")]
    public void GivenClientHasExpiredAuthorizationToken()
    {
        _testClient.AuthorizationToken = _environment.JwtAuthority.CreateExpiredToken(_testUserId);
    }
}
