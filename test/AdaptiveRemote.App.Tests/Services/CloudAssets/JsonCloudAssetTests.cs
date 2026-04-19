using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace AdaptiveRemote.Services.CloudAssets;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TestAsset))]
internal partial class TestAssetJsonContext : JsonSerializerContext { }

internal record TestAsset(string Name, int Value);

[TestClass]
public class JsonCloudAssetTests
{
    private static JsonCloudAsset<TestAsset> MakeSut() =>
        new("asset", "/stream", "asset-ready", "/assets",
            TestAssetJsonContext.Default);

    [TestMethod]
    public async Task JsonCloudAsset_DeserializeAsync_CorrectlyDeserializesAsync()
    {
        // Arrange
        TestAsset expected = new("test-name", 42);
        string json = JsonSerializer.Serialize(expected, TestAssetJsonContext.Default.TestAsset);
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
        JsonCloudAsset<TestAsset> sut = MakeSut();

        // Act
        object result = await sut.DeserializeAsync(stream, CancellationToken.None);

        // Assert
        result.Should().BeOfType<TestAsset>();
        TestAsset asset = (TestAsset)result;
        asset.Name.Should().Be("test-name");
        asset.Value.Should().Be(42);
    }
}
