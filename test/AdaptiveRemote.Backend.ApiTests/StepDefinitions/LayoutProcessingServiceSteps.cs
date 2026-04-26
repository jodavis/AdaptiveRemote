using System.Text.Json;
using AdaptiveRemote.Backend.ApiTests.Support;
using AdaptiveRemote.Contracts;
using FluentAssertions;
using Reqnroll;

namespace AdaptiveRemote.Backend.ApiTests.StepDefinitions;

[Binding]
public class LayoutProcessingServiceSteps
{
    private readonly ServiceContext _context;

    public LayoutProcessingServiceSteps(ServiceContext context)
    {
        _context = context;
    }

    [Given(@"LayoutProcessingService is running")]
    public async Task GivenLayoutProcessingServiceIsRunning()
    {
        await _context.Fixture.StartServiceAsync("AdaptiveRemote.Backend.LayoutProcessingService");
    }

    [Then(@"the body contains the LayoutProcessingService name and version")]
    public void ThenTheBodyContainsLayoutProcessingServiceNameAndVersion()
    {
        _context.LastResponseBody.Should().NotBeNullOrEmpty();

        HealthResponse? healthResponse = JsonSerializer.Deserialize<HealthResponse>(
            _context.LastResponseBody!,
            LayoutContractsJsonContext.Default.HealthResponse);

        healthResponse.Should().NotBeNull();
        healthResponse!.ServiceName.Should().Be("LayoutProcessingService");
        healthResponse.Version.Should().NotBeNullOrEmpty();
        healthResponse.Status.Should().Be("Healthy");
    }
}
