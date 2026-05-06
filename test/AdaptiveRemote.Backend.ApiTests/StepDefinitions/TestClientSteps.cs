using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AdaptiveRemote.Contracts;
using AdaptiveRemote.TestUtilities;
using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using Reqnroll;
using Reqnroll.Formatters.PayloadProcessing.Cucumber;

namespace AdaptiveRemote.Backend.ApiTests.StepDefinitions;

[Binding]
public class TestClientSteps
{
    private readonly TestClient _client;
    private HttpResponseMessage? _lastResponse;
    private string? _lastResponseBody;
    private object? _lastDeserializedObject;
    private Guid _existingRawLayoutId;

    public TestClientSteps(TestClient client)
    {
        _client = client;
    }

    [Given("{Uri} has a raw layout with the name {string}")]
    public void GivenARawLayoutExistsWithTheName(Uri endpointUri, string layoutName)
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

        WhenTheClientCallsEndpoint(HttpMethod.Post, new("/layouts/raw", UriKind.Relative), endpointUri, requestBody);
        ThenTheResponseIs(HttpStatusCode.Created);
        ThenTheResponseBodyRepresents(RawLayoutToJsonTypeInfo());

        _existingRawLayoutId = ((RawLayout)_lastDeserializedObject!).Id;
    }

    [When(@"the client calls (GET|POST|PUT|DELETE) (/\S+) on the (\w+) endpoint")]
    public void WhenTheClientCallsEndpoint(HttpMethod method, Uri url, Uri endpointUrl)
    {
        url = ProcessSpecialUris(url);

        _lastResponse = _client.SendRequest(method, new Uri(endpointUrl, url));
        _lastResponseBody = _lastResponse?.ReadContentAsString();
        _lastDeserializedObject = null;
    }

    [StepArgumentTransformation(@"/layouts/raw/\{id\}")]
    private Uri TransformRawLayoutId()
        => new Uri($"/layouts/raw/{_existingRawLayoutId}", UriKind.Relative);

    private Uri ProcessSpecialUris(Uri uri)
        => uri.ToString() switch
        {
            "/layouts/raw/{id}" => new Uri($"/layouts/raw/{_existingRawLayoutId}", UriKind.Relative),
            "/layouts/raw/{random}" => new Uri($"/layouts/raw/{Guid.NewGuid()}", UriKind.Relative),
            _ => uri
        };

    [When(@"the client calls (GET|POST|PUT|DELETE) (/\S+) on the (\w+) endpoint with")]
    public void WhenTheClientCallsEndpoint(HttpMethod method, Uri url, Uri endpointUrl, string body)
    {
        url = ProcessSpecialUris(url);

        _lastResponse = _client.SendRequest(method, new Uri(endpointUrl, url), body);
        _lastResponseBody = _lastResponse?.ReadContentAsString();
        _lastDeserializedObject = null;
    }

    [When(@"a raw layout named {string} is created via the {Uri}")]
    public void WhenARawLayoutIsCreatedViaTheEndpoint(string layoutName, Uri endpointUri)
    {
        GivenARawLayoutExistsWithTheName(endpointUri, layoutName);
    }

    [Then(@"the response is {HttpStatusCode}")]
    public void ThenTheResponseIs(HttpStatusCode expectedStatusCode)
    {
        Assert.IsNotNull(_lastResponse, "There hasn't been a request yet.");
        Assert.AreEqual(expectedStatusCode, _lastResponse.StatusCode, "Status code from the latest response. Response body:\n{0}", _lastResponseBody);
    }

    [Then(@"the response body is {string}")]
    public void ThenTheResponseBodyIs(string expectedBody)
    {
        Assert.IsNotNull(_lastResponseBody, "There hasn't been a request yet.");
        Assert.AreEqual(expectedBody, _lastResponseBody, "Latest response body");
    }

    [Then(@"the response body contains {string}")]
    public void ThenTheResponseBodyContains(string expectedContent)
    {
        Assert.IsNotNull(_lastResponseBody, "There hasn't been a request yet.");
        StringAssert.Contains(_lastResponseBody!, expectedContent, "Latest response body");
    }

    [Then(@"the response body does not contain {string}")]
    public void ThenTheResponseBodyDoesNotContain(string unexpectedContent)
    {
        Assert.IsNotNull(_lastResponseBody, "There hasn't been a request yet.");
        StringAssert.DoesNotMatch(_lastResponseBody!, new(unexpectedContent), "Latest response body");
    }

    [Then(@"the response body is valid JSON")]
    public void ThenTheResponseBodyIsValidJson()
    {
        Assert.IsNotNull(_lastResponseBody, "There hasn't been a request yet.");
        try
        {
            JsonDocument.Parse(_lastResponseBody!);
        }
        catch (JsonException ex)
        {
            Assert.Fail($"Response body is not valid JSON. Parsing error: {ex.Message}\nResponse body:\n{_lastResponseBody}");
        }
    }

    [Then(@"the response body represents a {JsonTypeInfo}")]
    public void ThenTheResponseBodyRepresents(JsonTypeInfo type)
    {
        Assert.IsNotNull(_lastResponseBody, "There hasn't been a request yet.");
        try
        {
            _lastDeserializedObject = JsonSerializer.Deserialize(_lastResponseBody!, type);
        }
        catch (JsonException ex)
        {
            Assert.Fail($"Response body could not be deserialized into {type.Type.Name}. Parsing error: {ex.Message}\nResponse body:\n{_lastResponseBody}");
        }
    }

    [Then(@"the CompiledLayout in the response body has a {CommandType} command named {string}")]
    public void ThenTheCompiledLayoutInTheResponseBodyHasACommandOfTypeWithName(CommandType expectedType, string expectedName)
    {
        Assert.IsNotNull(_lastDeserializedObject, "The response body has not been deserialized yet. Ensure that the step 'the response body represents a CompiledLayout' is called before this step.");
        Assert.IsInstanceOfType<CompiledLayout>(_lastDeserializedObject, "Expected the deserialized object to be a CompiledLayout.");
        CompiledLayout layout = (CompiledLayout)_lastDeserializedObject;

        IEnumerable<CommandDefinitionDto> commands = EnumerateAllCommands(layout.Elements);
        Assert.IsTrue(commands.Any(c => c.Type == expectedType && c.Name == expectedName),
            $"Expected to find a command of type {expectedType} with name '{expectedName}' in the CompiledLayout, but it was not found. Commands found: {string.Join(", ", commands.Select(c => $"{c.Type}:{c.Name}"))}");
    }

    [Then(@"the RawLayout in the response body has a valid Id property")]
    public void ThenTheRawLayoutInTheResponseBodyHasAValidIdProperty()
    {
        Assert.IsNotNull(_lastDeserializedObject, "The response body has not been deserialized yet. Ensure that the step 'the response body represents a RawLayout' is called before this step.");
        Assert.IsInstanceOfType<RawLayout>(_lastDeserializedObject, "Expected the deserialized object to be a RawLayout.");

        RawLayout layout = (RawLayout)_lastDeserializedObject;

        Assert.IsFalse(layout.Id == Guid.Empty, "Expected RawLayout to have a non-empty Id property.");
    }

    [Then(@"the {JsonTypeInfo} in the response body has a {string} property")]
    public void ThenTheDeserializedResponseHasAPropertyWithValue(JsonTypeInfo typeInfo, string propertyName)
    {
        Assert.IsNotNull(_lastDeserializedObject, "The response body has not been deserialized yet. Ensure that the step 'the response body represents a {JsonTypeInfo}' is called before this step.");
        Assert.IsInstanceOfType(_lastDeserializedObject, typeInfo.Type, $"Expected the deserialized object to be of type {typeInfo.Type.Name}.");

        JsonPropertyInfo? property = typeInfo.Properties.FirstOrDefault(x => x.Name == propertyName);
        Assert.IsNotNull(property, "{0} does not have a property named '{1}'. Found properties: {2}", typeInfo.Type.Name, propertyName, string.Join(", ", typeInfo.Properties.Select(p => p.Name)));
    }

    [Then(@"the {JsonTypeInfo} in the response body has {string}={string}")]
    public void ThenTheDeserializedResponseHasAPropertyWithValue(JsonTypeInfo typeInfo, string propertyName, string expectedValue)
    {
        Assert.IsNotNull(_lastDeserializedObject, "The response body has not been deserialized yet. Ensure that the step 'the response body represents a {JsonTypeInfo}' is called before this step.");
        Assert.IsInstanceOfType(_lastDeserializedObject, typeInfo.Type, $"Expected the deserialized object to be of type {typeInfo.Type.Name}.");

        JsonPropertyInfo? property = typeInfo.Properties.FirstOrDefault(x => x.Name == propertyName);
        Assert.IsNotNull(property, "{0} does not have a property named '{1}'. Found properties: {2}", typeInfo.Type.Name, propertyName, string.Join(", ", typeInfo.Properties.Select(p => p.Name)));
        Assert.AreEqual(typeof(string), property.PropertyType, "Expected property '{0}' to be of type string.", propertyName);

        Assert.IsNotNull(property.Get, "Property '{0}' does not have a Get method.", propertyName);
        object? value = property.Get(_lastDeserializedObject);

        Assert.IsNotNull(value, "Property '{0}' was null.", propertyName);
        Assert.AreEqual(expectedValue, value.ToString(), "Expected property '{0}' to have value '{1}', but found '{2}'.", propertyName, expectedValue, value);
    }

    [StepArgumentTransformation("(GET|POST|PUT|DELETE)")]
    public static HttpMethod StringToHttpMethod(string method)
        => method switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            _ => throw new ArgumentException($"Unsupported HTTP method: {method}")
        };

    [StepArgumentTransformation("(TiVo|IR|Lifecycle)")]
    public static CommandType StringToCommandType(string commandType)
        => Enum.Parse<CommandType>(commandType);

    [StepArgumentTransformation("200 OK")]
    public static HttpStatusCode StringToOk() => HttpStatusCode.OK;
    [StepArgumentTransformation("401 Unauthorized")]
    public static HttpStatusCode StringToUnauthorized() => HttpStatusCode.Unauthorized;
    [StepArgumentTransformation("201 Created")]
    public static HttpStatusCode StringToCreated() => HttpStatusCode.Created;
    [StepArgumentTransformation("204 No Content")]
    public static HttpStatusCode StringToNoContent() => HttpStatusCode.NoContent;
    [StepArgumentTransformation("404 Not Found")]
    public static HttpStatusCode StringToNotFound() => HttpStatusCode.NotFound;
    [StepArgumentTransformation("400 Bad Request")]
    public static HttpStatusCode StringToBadRequest() => HttpStatusCode.BadRequest;
    [StepArgumentTransformation("500 Internal Server Error")]
    public static HttpStatusCode StringToInternalServerError() => HttpStatusCode.InternalServerError;

    [StepArgumentTransformation(nameof(CompiledLayout))]
    public static JsonTypeInfo CompiledLayoutJsonTypeInfo() => LayoutContractsJsonContext.Default.CompiledLayout;
    [StepArgumentTransformation(nameof(HealthResponse))]
    public static JsonTypeInfo HealthResponseToJsonTypeInfo() => LayoutContractsJsonContext.Default.HealthResponse;
    [StepArgumentTransformation(nameof(RawLayout))]
    public static JsonTypeInfo RawLayoutToJsonTypeInfo() => LayoutContractsJsonContext.Default.RawLayout;

    private static IEnumerable<CommandDefinitionDto> EnumerateAllCommands(IEnumerable<LayoutElementDto> elements)
    {
        Stack<IEnumerator<LayoutElementDto>> stack = new();
        stack.Push(elements.GetEnumerator());

        while (stack.Count > 0)
        {
            IEnumerator<LayoutElementDto> enumerator = stack.Pop();
            while (enumerator.MoveNext())
            {
                LayoutElementDto current = enumerator.Current;
                if (current is CommandDefinitionDto command)
                {
                    yield return command;
                }
                else if (current is LayoutGroupDefinitionDto container)
                {
                    stack.Push(enumerator);
                    enumerator = container.Children.GetEnumerator();
                }
            }
        }
    }
}
