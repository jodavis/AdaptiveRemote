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
    private readonly ServiceFixture _fixture = new();
    private HttpResponseMessage? _response;
    private string? _responseBody;

    [Given(@"CompiledLayoutService is running")]
    public void GivenCompiledLayoutServiceIsRunning()
    {
        _fixture.StartService();
    }

    [When(@"a test client calls GET (.*)")]
    public async Task WhenATestClientCallsGet(string endpoint)
    {
        _response = await _fixture.HttpClient.GetAsync(endpoint);
        _responseBody = await _response.Content.ReadAsStringAsync();
    }

    [Then(@"the response is (\d+) OK")]
    public void ThenTheResponseIsOk(int statusCode)
    {
        _response.Should().NotBeNull();
        ((int)_response!.StatusCode).Should().Be(statusCode);
    }

    [Then(@"the body deserializes to a valid CompiledLayout using LayoutContractsJsonContext")]
    public void ThenTheBodyDeserializesToValidCompiledLayout()
    {
        _responseBody.Should().NotBeNullOrEmpty();

        CompiledLayout? layout = JsonSerializer.Deserialize<CompiledLayout>(
            _responseBody!,
            LayoutContractsJsonContext.Default.CompiledLayout);

        layout.Should().NotBeNull();
        layout!.Id.Should().NotBeEmpty();
        layout.Elements.Should().NotBeEmpty();
    }

    [Then(@"the CompiledLayout contains the expected hardcoded commands")]
    public void ThenTheCompiledLayoutContainsExpectedCommands()
    {
        _responseBody.Should().NotBeNullOrEmpty();

        CompiledLayout? layout = JsonSerializer.Deserialize<CompiledLayout>(
            _responseBody!,
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
        string logs = _fixture.GetLogs();
        logs.Should().Contain(endpoint);
    }

    [Then(@"the service logs contain no warnings or errors")]
    public void ThenTheServiceLogsContainNoWarningsOrErrors()
    {
        string logs = _fixture.GetLogs();
        logs.Should().NotContain("WARNING", "service should not log warnings");
        logs.Should().NotContain("ERROR", "service should not log errors");
        logs.Should().NotContain("Exception", "service should not log exceptions");
    }

    [Then(@"the body contains the service name and version")]
    public void ThenTheBodyContainsServiceNameAndVersion()
    {
        _responseBody.Should().NotBeNullOrEmpty();
        _responseBody.Should().Contain("CompiledLayoutService");
        _responseBody.Should().Contain("Version");
        _responseBody.Should().Contain("healthy");
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
        _response?.Dispose();
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
