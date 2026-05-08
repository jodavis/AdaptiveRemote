using AdaptiveRemote.EndToEndTests.TestServices;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Backend;

[Binding]
public class AuthenticationSteps : StepsBase
{
    // Use a unique user ID per fixture so each scenario operates on isolated data
    // even when DynamoDB is shared across test scenarios via the shared LocalStack.
    private readonly string _testUserId = $"test-user-{Guid.NewGuid():N}";

    [Given("the client has a valid Authorization token")]
    public void GivenClientHasValidAuthenticationToken()
    {
        TestClient.AuthorizationToken = Environment.JwtAuthority.CreateToken(_testUserId);
    }

    [Given("the client has no Authorization token")]
    public void GivenClientHasNoAuthorizationToken()
    {
        TestClient.AuthorizationToken = string.Empty;
    }

    [Given("the client has an expired Authorization token")]
    public void GivenClientHasExpiredAuthorizationToken()
    {
        TestClient.AuthorizationToken = Environment.JwtAuthority.CreateExpiredToken(_testUserId);
    }
}
