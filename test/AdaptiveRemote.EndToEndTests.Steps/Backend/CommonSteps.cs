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
    public void GivenServiceIsRunning(string serviceName)
    {
        _ = GetNamedService(serviceName); // Accessing the property ensures the service is started.
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
