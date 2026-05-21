using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AdaptiveRemote.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Backend;

[Binding]
public class LayoutCompilerSteps : StepsBase
{
    private RawLayout? _storedRawLayout;
    private IReadOnlyList<RawLayoutElementDto>? _storedElements;

    [StepArgumentTransformation(nameof(PreviewLayout))]
    public static JsonTypeInfo PreviewLayoutToJsonTypeInfo() => LayoutContractsJsonContext.Default.PreviewLayout;

    [Given(@"a RawLayout with a command element at grid position \((\d+), (\d+)\) is stored")]
    public void GivenARawLayoutWithCommandElementAtGridPosition(int gridRow, int gridColumn)
    {
        _storedRawLayout = new RawLayout(
            Id: Guid.NewGuid(),
            UserId: "test-user",
            Name: "Test Layout",
            Elements: new List<RawLayoutElementDto>
            {
                new RawCommandDefinitionDto(
                    Type: CommandType.TiVo,
                    Name: "Up",
                    Label: "Up",
                    Glyph: null,
                    SpeakPhrase: "up",
                    Reverse: null,
                    CssId: "cmd-up",
                    GridRow: gridRow,
                    GridColumn: gridColumn)
            },
            Version: 1,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            ValidationResult: null
        );
    }

    [Given(@"a list of RawLayoutElementDto with a command element at grid position \((\d+), (\d+)\) is stored")]
    public void GivenARawLayoutElementDtoListWithCommandElementAtGridPosition(int gridRow, int gridColumn)
    {
        _storedElements = new List<RawLayoutElementDto>
        {
            new RawCommandDefinitionDto(
                Type: CommandType.TiVo,
                Name: "Up",
                Label: "Up",
                Glyph: null,
                SpeakPhrase: "up",
                Reverse: null,
                CssId: "cmd-up",
                GridRow: gridRow,
                GridColumn: gridColumn)
        };
    }

    [When(@"the test client calls POST /compile on the (\w+) endpoint with the stored RawLayout")]
    public void WhenTestClientCallsPostCompileWithStoredRawLayout(Uri endpointUrl)
    {
        Assert.IsNotNull(_storedRawLayout, "No RawLayout has been stored. Call the Given step first.");
        string json = JsonSerializer.Serialize(_storedRawLayout, LayoutContractsJsonContext.Default.RawLayout);
        TestClient.SendRequest(HttpMethod.Post, new Uri(endpointUrl, "/compile"), json);
    }

    [When(@"the test client calls POST /compile/preview on the (\w+) endpoint with the stored elements")]
    public void WhenTestClientCallsPostCompilePreviewWithStoredElements(Uri endpointUrl)
    {
        Assert.IsNotNull(_storedElements, "No elements have been stored. Call the Given step first.");
        string json = JsonSerializer.Serialize(_storedElements, LayoutContractsJsonContext.Default.IReadOnlyListRawLayoutElementDto);
        TestClient.SendRequest(HttpMethod.Post, new Uri(endpointUrl, "/compile/preview"), json);
    }

    [Then(@"the CompiledLayout contains a CommandDefinitionDto with name {string}")]
    public void ThenTheCompiledLayoutContainsACommandDefinitionDtoWithName(string expectedName)
    {
        CompiledLayout? layout = TestClient.LastResponseObject as CompiledLayout;
        Assert.IsNotNull(layout, "Response was not parsed as a CompiledLayout. Call 'the response body represents a CompiledLayout' first.");

        bool found = layout.Elements.OfType<CommandDefinitionDto>().Any(c => c.Name == expectedName);
        Assert.IsTrue(found, "Expected CompiledLayout.Elements to contain a CommandDefinitionDto with Name='{0}'.", expectedName);
    }

    [Then(@"the CompiledLayout CSS definitions contain a rule for grid position \((\d+), (\d+)\)")]
    public void ThenTheCompiledLayoutCssDefinitionsContainARuleForGridPosition(int gridRow, int gridColumn)
    {
        CompiledLayout? layout = TestClient.LastResponseObject as CompiledLayout;
        Assert.IsNotNull(layout, "Response was not parsed as a CompiledLayout.");
        Assert.IsFalse(string.IsNullOrEmpty(layout.CssDefinitions), "CompiledLayout.CssDefinitions is empty.");
        Assert.IsTrue(
            layout.CssDefinitions.Contains($"grid-row: {gridRow}") && layout.CssDefinitions.Contains($"grid-column: {gridColumn}"),
            "Expected CssDefinitions to contain 'grid-row: {0}' and 'grid-column: {1}', but was:\n{2}",
            gridRow, gridColumn, layout.CssDefinitions);
    }

    [Then(@"the CompiledLayout does not contain authoring properties on its elements")]
    public void ThenTheCompiledLayoutDoesNotContainAuthoringPropertiesOnElements()
    {
        string body = TestClient.LastResponseBody;
        Assert.IsFalse(body.Contains("\"gridRow\""), "CompiledLayout JSON should not contain 'gridRow' on elements.");
        Assert.IsFalse(body.Contains("\"gridColumn\""), "CompiledLayout JSON should not contain 'gridColumn' on elements.");
        Assert.IsFalse(body.Contains("\"additionalCss\""), "CompiledLayout JSON should not contain 'additionalCss' on elements.");
    }

    [Then(@"the PreviewLayout RenderedHtml is non-empty")]
    public void ThenThePreviewLayoutRenderedHtmlIsNonEmpty()
    {
        PreviewLayout? preview = TestClient.LastResponseObject as PreviewLayout;
        Assert.IsNotNull(preview, "Response was not parsed as a PreviewLayout. Call 'the response body represents a PreviewLayout' first.");
        Assert.IsFalse(string.IsNullOrEmpty(preview.RenderedHtml), "PreviewLayout.RenderedHtml is empty.");
    }

    [Then(@"the PreviewLayout RenderedCss is non-empty")]
    public void ThenThePreviewLayoutRenderedCssIsNonEmpty()
    {
        PreviewLayout? preview = TestClient.LastResponseObject as PreviewLayout;
        Assert.IsNotNull(preview, "Response was not parsed as a PreviewLayout.");
        Assert.IsFalse(string.IsNullOrEmpty(preview.RenderedCss), "PreviewLayout.RenderedCss is empty.");
    }
}
