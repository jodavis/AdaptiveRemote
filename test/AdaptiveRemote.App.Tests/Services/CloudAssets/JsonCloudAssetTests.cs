using System.Text;
using System.Text.Json;
using AdaptiveRemote.Contracts;
using FluentAssertions;

namespace AdaptiveRemote.Services.CloudAssets;

[TestClass]
public class JsonCloudAssetTests
{
    private static JsonCloudAsset<CompiledLayout> MakeSut() =>
        new("layout", "/stream", "layout-ready", "/layouts/compiled",
            LayoutContractsJsonContext.Default);

    [TestMethod]
    public async Task JsonCloudAsset_ParseAsync_CorrectlyDeserializesCompiledLayoutAsync()
    {
        // Arrange
        CompiledLayout expected = new(
            Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RawLayoutId: Guid.Empty,
            UserId: "test-user",
            IsActive: true,
            Version: 42,
            Elements: [
                new LayoutGroupDefinitionDto("GROUP", [
                    new CommandDefinitionDto(CommandType.TiVo, "Play", "Play", null, "Sent Play", "Pause", "Play"),
                ])
            ],
            CssDefinitions: "body { color: red; }",
            CompiledAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        string json = JsonSerializer.Serialize(expected, LayoutContractsJsonContext.Default.CompiledLayout);
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
        JsonCloudAsset<CompiledLayout> sut = MakeSut();

        // Act
        object result = await sut.ParseAsync(stream, CancellationToken.None);

        // Assert
        result.Should().BeOfType<CompiledLayout>();
        CompiledLayout layout = (CompiledLayout)result;
        layout.Id.Should().Be(expected.Id);
        layout.UserId.Should().Be("test-user");
        layout.Version.Should().Be(42);
        layout.CssDefinitions.Should().Be("body { color: red; }");
        layout.Elements.Should().HaveCount(1);
        layout.Elements[0].Should().BeOfType<LayoutGroupDefinitionDto>()
            .Which.CssId.Should().Be("GROUP");
    }
}
