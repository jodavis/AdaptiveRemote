using AdaptiveRemote.EndToEndTests.TestServices.Backend;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Backend;

[Binding]
public class ServiceSteps : StepsBase
{
    private const string ServiceRegex = "(RawLayoutService|CompiledLayoutService|LayoutProcessingService|LayoutCompilerService)";

    [StepArgumentTransformation(ServiceRegex)]
    public Uri ServiceNameToEndpointUri(string serviceName)
        => new(ServiceNameToFixture(serviceName).ServiceUrl);

    [StepArgumentTransformation(ServiceRegex)]
    public ServiceFixture ServiceNameToFixture(string serviceName)
        => serviceName switch
        {
            "RawLayoutService" => Environment.RawLayoutService,
            "CompiledLayoutService" => Environment.CompiledLayoutService,
            "LayoutProcessingService" => Environment.LayoutProcessingService,
            "LayoutCompilerService" => Environment.LayoutCompilerService,
            _ => throw new ArgumentException($"Unknown service name: {serviceName}", nameof(serviceName))
        };

    [Given(@"^" + ServiceRegex + " is running")]
    public void GivenCompiledLayoutServiceIsRunning(string serviceName)
    {
        // Accessing the property ensures the service is started.
        _ = ServiceNameToFixture(serviceName);
    }
}
