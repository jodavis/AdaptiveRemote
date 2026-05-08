using System.Text.Json.Serialization.Metadata;
using AdaptiveRemote.Contracts;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Backend;

[Binding]
internal static class HealthResponseSteps
{
    [StepArgumentTransformation(nameof(HealthResponse))]
    public static JsonTypeInfo HealthResponseToJsonTypeInfo() => LayoutContractsJsonContext.Default.HealthResponse;
}
