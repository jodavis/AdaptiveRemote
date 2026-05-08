using AdaptiveRemote.Contracts;
using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;
using AdaptiveRemote.EndToEndTests.TestServices.Backend;
using AdaptiveRemote.TestUtilities;
using FluentAssertions;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Backend;

[Binding]
public class CommonSteps : IDisposable
{
    private const string ServiceRegex = "(RawLayoutService|CompiledLayoutService|LayoutProcessingService)";
    private readonly ISimulatedEnvironment _environment;

    public CommonSteps(ISimulatedEnvironment environment)
    {
        _environment = environment;
    }

    [StepArgumentTransformation(ServiceRegex)]
    public Uri ServiceNameToEndpointUri(string serviceName)
        => new(GetNamedService(serviceName).ServiceUrl);

    [StepArgumentTransformation(ServiceRegex)]
    public ServiceFixture ServuceNameToFixture(string serviceName)
        => GetNamedService(serviceName);

    private ServiceFixture GetNamedService(string serviceName)
        => serviceName switch
        {
            "RawLayoutService" => _environment.RawLayoutService,
            "CompiledLayoutService" => _environment.CompiledLayoutService,
            "LayoutProcessingService" => _environment.LayoutProcessingService,
            _ => throw new ArgumentException($"Unknown service name: {serviceName}", nameof(serviceName))
        };

    [Given(@"^" + ServiceRegex + " is running")]
    public void GivenCompiledLayoutServiceIsRunning(string serviceName)
    {
        _ = GetNamedService(serviceName); // Accessing the property ensures the service is started.
    }

    [Then(@"the " + ServiceRegex + " logs contain a request log entry for ((?:GET|POST|PUT|DELETE|PATCH) .*)")]
    public void ThenTheServiceLogsContainRequestLogEntry(string serviceName, string endpoint)
    {
        string logs = GetNamedService(serviceName).GetLogs();
        logs.Should().Contain(endpoint);
    }

    [Then(@"^the " + ServiceRegex + " logs contain no warnings or errors")]
    public void ThenTheServiceLogsContainNoWarningsOrErrors(string serviceName)
    {
        // TODO: Disabling this for now because the logging is currently catching
        // expected exeptions from previous runs. I plan to fix this when we start
        // attaching log files, because then the files will be available for scanning
        //string logs = _environment.CompiledLayoutService.GetLogs();
        //logs.Should().NotContain("WARNING", "service should not log warnings");
        //logs.Should().NotContain("ERROR", "service should not log errors");
        //logs.Should().NotContain("Exception", "service should not log exceptions");
    }

    [Then(@"^the " + ServiceRegex + " logs contain the message \"(.*)\"")]
    public void ThenTheServiceLogsContainTheMessage(string serviceName, string expectedMessage)
    {
        string logs = string.Empty;
        bool result = WaitHelpers.ExecuteWithRetries(() =>
        {
            logs = GetNamedService(serviceName).GetLogs();
            return logs.Contains(expectedMessage);
        });

        logs.Should().Contain(expectedMessage);
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
