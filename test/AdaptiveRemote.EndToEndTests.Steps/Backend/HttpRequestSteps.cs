using System.Net;
using System.Text.Json;
using AdaptiveRemote.Contracts;
using AdaptiveRemote.EndToEndTests.TestServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Backend;

[Binding]
public class HttpRequestSteps : StepsBase
{
    private const string HttpMethodFilter = "(GET|POST|PUT|DELETE|PATCH)";
    private Guid? _existingRawLayoutId;

    [StepArgumentTransformation(HttpMethodFilter)]
    public static HttpMethod StringToHttpMethod(string method)
        => method switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            _ => throw new ArgumentException($"Unsupported HTTP method: {method}")
        };

    [StepArgumentTransformation(@"/layouts/raw/\{id\}")]
    private Uri TransformRawLayoutId()
        => new Uri($"/layouts/raw/{_existingRawLayoutId}", UriKind.Relative);

    [Given("{Uri} has a raw layout with the name {string}")]
    public void GivenARawLayoutExistsWithTheName(Uri endpointUri, string layoutName)
    {
        WhenANamedLayoutIsCreatedVia(layoutName, endpointUri);
    }

    [When(@"the client calls " + HttpMethodFilter + @" (/\S+) on the (\w+) endpoint")]
    public void WhenTheClientCallsEndpoint(HttpMethod method, Uri url, Uri endpointUrl)
    {
        WhenTheClientCallsEndpoint(method, url, endpointUrl, null);
    }

    [When(@"the client calls " + HttpMethodFilter + @" (/\S+) on the (\w+) endpoint with")]
    public void WhenTheClientCallsEndpoint(HttpMethod method, Uri url, Uri endpointUrl, string? body)
    {
        //url = ProcessSpecialUris(url);

        TestClient.SendRequest(method, new Uri(endpointUrl, url), body);
    }

    [When(@"a layout named {string} is created via {Uri}")]
    public void WhenANamedLayoutIsCreatedVia(string layoutName, Uri endpointUri)
    {
        RawLayout testLayout = new(
            Id: Guid.Empty,
            UserId: "test-user",
            Name: layoutName,
            Elements: new List<RawLayoutElementDto>
            {
                new RawCommandDefinitionDto(
                    Type: CommandType.TiVo,
                    Name: "Up",
                    Label: "Up",
                    Glyph: "↑",
                    SpeakPhrase: "up",
                    Reverse: "Down",
                    CssId: "up-btn",
                    GridRow: 0,
                    GridColumn: 1
                )
            },
            Version: 1,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            ValidationResult: null
        );

        string requestBody = JsonSerializer.Serialize(testLayout, LayoutContractsJsonContext.Default.RawLayout);

        WhenThisLayoutIsCreatedVia(endpointUri, requestBody);
    }

    [When(@"^this layout is created via (RawLayoutService):")]
    public void WhenThisLayoutIsCreatedVia(Uri serviceUri, string body)
    {
        WhenTheClientCallsEndpoint(HttpMethod.Post, new("/layouts/raw", UriKind.Relative), serviceUri, body);
        Assert.AreEqual(HttpStatusCode.Created, TestClient.LastResponse!.StatusCode, "Layout creation returned an unexpected status code.");
        
        TestClient.ParseResponseAs(LayoutContractsJsonContext.Default.RawLayout);
        _existingRawLayoutId = ((RawLayout)TestClient.LastResponseObject).Id;
    }
}
