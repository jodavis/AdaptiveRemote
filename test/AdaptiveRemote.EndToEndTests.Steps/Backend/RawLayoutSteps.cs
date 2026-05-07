using AdaptiveRemote.Contracts;
using AdaptiveRemote.EndToEndTests.TestServices.Backend;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Backend;

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
