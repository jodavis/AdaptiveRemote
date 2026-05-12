using System.Text.Json.Serialization.Metadata;
using AdaptiveRemote.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Backend;

[Binding]
public class RawLayoutSteps : StepsBase
{
    [StepArgumentTransformation(nameof(RawLayout))]
    public static JsonTypeInfo RawLayoutToJsonTypeInfo() => LayoutContractsJsonContext.Default.RawLayout;

    [Then(@"the RawLayout in the response body has a valid Id property")]
    public void ThenTheRawLayoutInTheResponseBodyHasAValidIdProperty()
    {
        RawLayout? layout = TestClient.LastResponseObject as RawLayout;
        Assert.IsNotNull(layout, "Last response was not parsed as a RawLayout");

        Assert.IsFalse(layout.Id == Guid.Empty, "Expected RawLayout to have a non-empty Id property.");
    }
}
