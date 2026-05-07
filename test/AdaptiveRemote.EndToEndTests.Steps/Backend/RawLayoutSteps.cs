using AdaptiveRemote.EndToEndTests.TestServices.Backend;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Backend;

[Binding]
public class RawLayoutSteps
{
    private readonly ServiceFixture _fixture;

    public RawLayoutSteps(ServiceFixture fixture)
    {
        _fixture = fixture;
    }

    [StepArgumentTransformation("RawLayoutService")]
    public Uri RawLayoutServiceToEndpointUri() => new(_fixture.ServiceUrl);

    [Given(@"RawLayoutService is running")]
    public void GivenRawLayoutServiceIsRunning()
    {
        _fixture.StartServiceAsync("AdaptiveRemote.Backend.RawLayoutService");
    }
}
