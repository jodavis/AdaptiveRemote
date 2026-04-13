using System.Net;
using System.Text.Json;
using AdaptiveRemote.Backend.ApiTests.Support;
using AdaptiveRemote.Contracts;
using FluentAssertions;
using Reqnroll;

namespace AdaptiveRemote.Backend.ApiTests.StepDefinitions;

[Binding]
public class CommonSteps : IDisposable
{
    private readonly ServiceContext _context;

    public CommonSteps(ServiceContext context)
    {
        _context = context;
    }

    [Given(@"CompiledLayoutService is running")]
    public async Task GivenCompiledLayoutServiceIsRunning()
    {
        await _context.Fixture.StartServiceAsync();
    }

    [When(@"a test client calls GET (.*)")]
    public async Task WhenATestClientCallsGet(string endpoint)
    {
        _context.LastResponse = await _context.Fixture.HttpClient.GetAsync(endpoint);
        _context.LastResponseBody = await _context.LastResponse.Content.ReadAsStringAsync();
    }

    [Then(@"the response is (\d+) OK")]
    public void ThenTheResponseIsOk(int statusCode)
    {
        _context.LastResponse.Should().NotBeNull();
        ((int)_context.LastResponse!.StatusCode).Should().Be(statusCode);
    }

    [Then(@"the body deserializes to a valid CompiledLayout using LayoutContractsJsonContext")]
    public void ThenTheBodyDeserializesToValidCompiledLayout()
    {
        _context.LastResponseBody.Should().NotBeNullOrEmpty();

        CompiledLayout? layout = JsonSerializer.Deserialize<CompiledLayout>(
            _context.LastResponseBody!,
            LayoutContractsJsonContext.Default.CompiledLayout);

        layout.Should().NotBeNull();
        layout!.Id.Should().NotBeEmpty();
        layout.Elements.Should().NotBeEmpty();
    }

    [Then(@"the CompiledLayout contains the expected hardcoded commands")]
    public void ThenTheCompiledLayoutContainsExpectedCommands()
    {
        _context.LastResponseBody.Should().NotBeNullOrEmpty();

        CompiledLayout? layout = JsonSerializer.Deserialize<CompiledLayout>(
            _context.LastResponseBody!,
            LayoutContractsJsonContext.Default.CompiledLayout);

        layout.Should().NotBeNull();

        // Verify key commands from StaticCommandGroupProvider exist
        List<CommandDefinitionDto> commands = ExtractAllCommands(layout!.Elements);

        commands.Should().Contain(c => c.Name == "Up" && c.Type == CommandType.TiVo);
        commands.Should().Contain(c => c.Name == "Select" && c.Type == CommandType.TiVo);
        commands.Should().Contain(c => c.Name == "Power" && c.Type == CommandType.IR);
        commands.Should().Contain(c => c.Name == "Learn" && c.Type == CommandType.Lifecycle);
        commands.Should().Contain(c => c.Name == "Exit" && c.Type == CommandType.Lifecycle);
    }

    [Then(@"the service logs contain a request log entry for GET (.*)")]
    public void ThenTheServiceLogsContainRequestLogEntry(string endpoint)
    {
        string logs = _context.Fixture.GetLogs();
        logs.Should().Contain(endpoint);
    }

    [Then(@"the service logs contain no warnings or errors")]
    public void ThenTheServiceLogsContainNoWarningsOrErrors()
    {
        string logs = _context.Fixture.GetLogs();
        logs.Should().NotContain("WARNING", "service should not log warnings");
        logs.Should().NotContain("ERROR", "service should not log errors");
        logs.Should().NotContain("Exception", "service should not log exceptions");
    }

    [Then(@"the body contains the service name and version")]
    public void ThenTheBodyContainsServiceNameAndVersion()
    {
        _context.LastResponseBody.Should().NotBeNullOrEmpty();

        HealthResponse? healthResponse = JsonSerializer.Deserialize<HealthResponse>(
            _context.LastResponseBody!,
            LayoutContractsJsonContext.Default.HealthResponse);

        healthResponse.Should().NotBeNull();
        healthResponse!.ServiceName.Should().Be("CompiledLayoutService");
        healthResponse.Version.Should().NotBeNullOrEmpty();
        healthResponse.Status.Should().Be("healthy");
    }

    private static List<CommandDefinitionDto> ExtractAllCommands(IReadOnlyList<LayoutElementDto> elements)
    {
        List<CommandDefinitionDto> commands = new();

        foreach (LayoutElementDto element in elements)
        {
            if (element is CommandDefinitionDto command)
            {
                commands.Add(command);
            }
            else if (element is LayoutGroupDefinitionDto group)
            {
                commands.AddRange(ExtractAllCommands(group.Children));
            }
        }

        return commands;
    }

    public void Dispose()
    {
        // ServiceContext owns LastResponse and Fixture; nothing to dispose here.
        GC.SuppressFinalize(this);
    }
}
