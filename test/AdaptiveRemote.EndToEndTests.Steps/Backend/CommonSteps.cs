using AdaptiveRemote.Contracts;
using AdaptiveRemote.EndToEndTests.TestServices.Backend;
using FluentAssertions;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Backend;

[Binding]
public class CommonSteps : IDisposable
{
    private readonly ServiceContext _context;

    public CommonSteps(ServiceContext context)
    {
        _context = context;
    }

    [StepArgumentTransformation("CompiledLayoutService")]
    public Uri CompiledLayoutServiceToEndpointUri()
        => new(_context.Fixture.ServiceUrl);

    [Given(@"CompiledLayoutService is running")]
    public async Task GivenCompiledLayoutServiceIsRunningAsync()
    {
        await _context.Fixture.StartServiceAsync();
    }

    [Then(@"the service logs contain a request log entry for (?:GET|POST|PUT|DELETE|PATCH) (.*)")]
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
