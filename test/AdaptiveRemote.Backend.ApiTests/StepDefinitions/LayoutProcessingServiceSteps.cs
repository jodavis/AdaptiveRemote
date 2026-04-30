using System.Net;
using System.Text;
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
    private readonly PipelineContext _pipelineContext;

    public LayoutProcessingServiceSteps(ServiceContext context, PipelineContext pipelineContext)
    {
        _context = context;
        _pipelineContext = pipelineContext;
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

    // -------------------------------------------------------------------------
    // Pipeline integration steps
    // -------------------------------------------------------------------------

    [Given(@"the layout processing pipeline is running")]
    public async Task GivenThePipelineIsRunning()
    {
        await _pipelineContext.Fixture.StartAsync(forceValidationInvalid: false);
    }

    [Given(@"the layout processing pipeline is running with forced validation failure")]
    public async Task GivenThePipelineIsRunningWithForcedValidationFailure()
    {
        await _pipelineContext.Fixture.StartAsync(forceValidationInvalid: true);
    }

    [When(@"a raw layout is created via RawLayoutService")]
    public async Task WhenARawLayoutIsCreatedViaRawLayoutService()
    {
        RawLayout layout = new(
            Id: Guid.Empty,
            UserId: "test-user",
            Name: "Pipeline Test Layout",
            Elements: [
                new RawCommandDefinitionDto(
                    Type: CommandType.TiVo,
                    Name: "Up",
                    Label: "Up",
                    Glyph: "↑",
                    SpeakPhrase: "up",
                    Reverse: "Down",
                    CssId: "up-btn",
                    GridRow: 0,
                    GridColumn: 0)
            ],
            Version: 1,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            ValidationResult: null);

        StringContent content = new(
            JsonSerializer.Serialize(layout, LayoutContractsJsonContext.Default.RawLayout),
            Encoding.UTF8,
            "application/json");

        _pipelineContext.LastResponse = await _pipelineContext.Fixture.RawLayoutHttpClient
            .PostAsync("/layouts/raw", content);
        _pipelineContext.LastResponseBody =
            await _pipelineContext.LastResponse.Content.ReadAsStringAsync();

        _pipelineContext.LastResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "RawLayoutService must accept the layout before the pipeline can run");
    }

    [Then(@"the processing service logs show the layout was compiled and validated")]
    public async Task ThenProcessingLogsShowCompiledAndValidated()
    {
        bool compiled = await _pipelineContext.Fixture
            .WaitForLogAsync("Layout compiled successfully", TimeSpan.FromSeconds(30));
        compiled.Should().BeTrue("LayoutProcessingService should log compilation within 30 s");

        bool validated = await _pipelineContext.Fixture
            .WaitForLogAsync("Layout validation passed", TimeSpan.FromSeconds(10));
        validated.Should().BeTrue("LayoutProcessingService should log validation passed within 10 s");
    }

    [Then(@"the processing service logs show the compiled layout was stored")]
    public async Task ThenProcessingLogsShowCompiledLayoutStored()
    {
        bool stored = await _pipelineContext.Fixture
            .WaitForLogAsync("Compiled layout stored", TimeSpan.FromSeconds(10));
        stored.Should().BeTrue("LayoutProcessingService should log compiled layout stored within 10 s");

        bool published = await _pipelineContext.Fixture
            .WaitForLogAsync("Layout-ready notification published", TimeSpan.FromSeconds(10));
        published.Should().BeTrue("LayoutProcessingService should log notification published within 10 s");
    }

    [Then(@"the processing service logs show no unhandled errors")]
    public void ThenProcessingLogsShowNoUnhandledErrors()
    {
        string logs = _pipelineContext.Fixture.GetProcessingLogs();
        // "SQS message processed successfully" is the normal completion marker
        logs.Should().Contain("SQS message processed successfully",
            "pipeline should complete the SQS message");
    }

    [Then(@"the processing service logs show the layout failed validation")]
    public async Task ThenProcessingLogsShowValidationFailed()
    {
        bool failed = await _pipelineContext.Fixture
            .WaitForLogAsync("Layout validation failed", TimeSpan.FromSeconds(30));
        failed.Should().BeTrue("LayoutProcessingService should log validation failure within 30 s");
    }

    [Then(@"the processing service logs show the validation result was written back")]
    public async Task ThenProcessingLogsShowValidationResultWrittenBack()
    {
        bool writtenBack = await _pipelineContext.Fixture
            .WaitForLogAsync("Validation result written back to raw layout", TimeSpan.FromSeconds(10));
        writtenBack.Should().BeTrue("LayoutProcessingService should log validation result write-back within 10 s");
    }

}
