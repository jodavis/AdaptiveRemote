using AdaptiveRemote.Backend.ApiTests.Support;
using AdaptiveRemote.Contracts;
using Reqnroll;

namespace AdaptiveRemote.Backend.ApiTests.StepDefinitions;

[Binding]
public class RawLayoutSteps
{
    private readonly ServiceContext _context;

    public RawLayoutSteps(ServiceContext context)
    {
        _context = context;
    }

    [StepArgumentTransformation("RawLayoutService")]
    public Uri RawLayoutServiceToEndpointUri() => new(_context.Fixture.ServiceUrl);

    [Given(@"RawLayoutService is running")]
    public async Task GivenRawLayoutServiceIsRunning()
    {
        await _context.Fixture.StartServiceAsync("AdaptiveRemote.Backend.RawLayoutService");
    }
}
