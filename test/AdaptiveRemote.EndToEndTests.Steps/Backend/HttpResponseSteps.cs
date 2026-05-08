using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AdaptiveRemote.EndToEndTests.TestServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Backend;

[Binding]
public class HttpResponseSteps : StepsBase
{
    [StepArgumentTransformation("200 OK")] public static HttpStatusCode StringToOk() => HttpStatusCode.OK;
    [StepArgumentTransformation("401 Unauthorized")] public static HttpStatusCode StringToUnauthorized() => HttpStatusCode.Unauthorized;
    [StepArgumentTransformation("201 Created")] public static HttpStatusCode StringToCreated() => HttpStatusCode.Created;
    [StepArgumentTransformation("204 No Content")] public static HttpStatusCode StringToNoContent() => HttpStatusCode.NoContent;
    [StepArgumentTransformation("404 Not Found")] public static HttpStatusCode StringToNotFound() => HttpStatusCode.NotFound;
    [StepArgumentTransformation("400 Bad Request")] public static HttpStatusCode StringToBadRequest() => HttpStatusCode.BadRequest;
    [StepArgumentTransformation("500 Internal Server Error")] public static HttpStatusCode StringToInternalServerError() => HttpStatusCode.InternalServerError;

    [Then(@"the response is {HttpStatusCode}")]
    public void ThenTheResponseIs(HttpStatusCode expectedStatusCode)
    {
        Assert.AreEqual(expectedStatusCode, TestClient.LastResponse.StatusCode, "Status code from the latest response. Response body:\n{0}", TestClient.LastResponseBody);
    }

    [Then(@"the response body is {string}")]
    public void ThenTheResponseBodyIs(string expectedBody)
    {
        Assert.AreEqual(expectedBody, TestClient.LastResponseBody, "Latest response body");
    }

    [Then(@"the response body contains {string}")]
    public void ThenTheResponseBodyContains(string expectedContent)
    {
        StringAssert.Contains(TestClient.LastResponseBody, expectedContent, "Latest response body");
    }

    [Then(@"the response body does not contain {string}")]
    public void ThenTheResponseBodyDoesNotContain(string unexpectedContent)
    {
        StringAssert.DoesNotMatch(TestClient.LastResponseBody!, new(unexpectedContent), "Latest response body");
    }

    [Then(@"the response body is valid JSON")]
    public void ThenTheResponseBodyIsValidJson()
    {
        try
        {
            JsonDocument.Parse(TestClient.LastResponseBody);
        }
        catch (JsonException ex)
        {
            Assert.Fail($"Response body is not valid JSON. Parsing error: {ex.Message}\nResponse body:\n{TestClient.LastResponseBody}");
        }
    }

    [Then(@"the response body represents a {JsonTypeInfo}")]
    public void ThenTheResponseBodyRepresents(JsonTypeInfo type)
    {
        try
        {
            TestClient.ParseResponseAs(type);
        }
        catch (JsonException ex)
        {
            Assert.Fail($"Response body could not be deserialized into {type.Type.Name}. Parsing error: {ex.Message}\nResponse body:\n{TestClient.LastResponseBody}");
        }
    }

    [Then(@"the {JsonTypeInfo} in the response body has a {string} property")]
    public void ThenTheDeserializedResponseHasAPropertyWithValue(JsonTypeInfo typeInfo, string propertyName)
    {
        Assert.IsInstanceOfType(TestClient.LastResponseObject, typeInfo.Type, $"Expected the deserialized object to be of type {typeInfo.Type.Name}.");

        JsonPropertyInfo? property = typeInfo.Properties.FirstOrDefault(x => x.Name == propertyName);
        Assert.IsNotNull(property, "{0} does not have a property named '{1}'. Found properties: {2}", typeInfo.Type.Name, propertyName, string.Join(", ", typeInfo.Properties.Select(p => p.Name)));
    }

    [Then(@"the {JsonTypeInfo} in the response body has {string}={string}")]
    public void ThenTheDeserializedResponseHasAPropertyWithValue(JsonTypeInfo typeInfo, string propertyName, string expectedValue)
    {
        Assert.IsInstanceOfType(TestClient.LastResponseObject, typeInfo.Type, $"Expected the deserialized object to be of type {typeInfo.Type.Name}.");

        JsonPropertyInfo? property = typeInfo.Properties.FirstOrDefault(x => x.Name == propertyName);
        Assert.IsNotNull(property, "{0} does not have a property named '{1}'. Found properties: {2}", typeInfo.Type.Name, propertyName, string.Join(", ", typeInfo.Properties.Select(p => p.Name)));
        Assert.AreEqual(typeof(string), property.PropertyType, "Expected property '{0}' to be of type string.", propertyName);

        Assert.IsNotNull(property.Get, "Property '{0}' does not have a Get method.", propertyName);
        object? value = property.Get(TestClient.LastResponseObject);

        Assert.IsNotNull(value, "Property '{0}' was null.", propertyName);
        Assert.AreEqual(expectedValue, value.ToString(), "Expected property '{0}' to have value '{1}', but found '{2}'.", propertyName, expectedValue, value);
    }
}
