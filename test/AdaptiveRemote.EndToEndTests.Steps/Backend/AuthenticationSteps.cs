using AdaptiveRemote.EndToEndTests.TestServices;
using AdaptiveRemote.EndToEndTests.TestServices.Backend;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Backend;

[Binding]
public class AuthenticationSteps
{
    private readonly ServiceFixture _fixture;
    private readonly TestClient _testClient;

    public AuthenticationSteps(ServiceFixture fixture, TestClient testClient)
    {
        _fixture = fixture;
        _testClient = testClient;
    }

    [Given("the client has a valid Authorization token")]
    public void GivenClientHasValidAuthenticationToken()
    {
        _testClient.AuthorizationToken = _fixture.CreateToken();
    }

    [Given("the client has a no Authorization token")]
    public void GivenClientHasNoAuthorizationToken()
    {
        _testClient.AuthorizationToken = string.Empty;
    }

    [Given("the client has an expired Authorization token")]
    public void GivenClientHasExpiredAuthorizationToken()
    {
        _testClient.AuthorizationToken = _fixture.CreateExpiredToken();
    }
}
