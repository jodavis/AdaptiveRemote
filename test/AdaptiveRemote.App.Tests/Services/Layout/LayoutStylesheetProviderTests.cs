using AdaptiveRemote.Contracts;
using AdaptiveRemote.Services.CloudAssets;
using FluentAssertions;
using Moq;

namespace AdaptiveRemote.Services.Layout;

[TestClass]
public class LayoutStylesheetProviderTests
{
    private static CompiledLayout MakeLayout(string cssDefinitions) =>
        new(Guid.Empty, Guid.Empty, "stub", true, 1, [], cssDefinitions, DateTimeOffset.UtcNow);

    [TestMethod]
    public void LayoutStylesheetProvider_GetCss_ReturnsCssFromCompiledLayout()
    {
        // Arrange
        const string expectedCss = "#ROOT { display: grid; grid-template-columns: 3fr 2fr; }";
        CompiledLayout layout = MakeLayout(expectedCss);

        Mock<ICloudAssetStore> storeMock = new(MockBehavior.Strict);
        storeMock.Setup(s => s.Get<CompiledLayout>("layout")).Returns(layout);

        LayoutStylesheetProvider sut = new(storeMock.Object);

        // Act
        string? css = sut.GetCss();

        // Assert
        css.Should().Be(expectedCss);
    }

    [TestMethod]
    public void LayoutStylesheetProvider_GetCss_EmptyStoreThrowsDescriptiveError()
    {
        // Arrange
        Mock<ICloudAssetStore> storeMock = new(MockBehavior.Strict);
        storeMock.Setup(s => s.Get<CompiledLayout>("layout"))
            .Throws(new InvalidOperationException("Asset 'layout' not found in store. Ensure CloudAssetOrchestrator has completed before accessing assets."));

        LayoutStylesheetProvider sut = new(storeMock.Object);

        // Act
        Action act = () => sut.GetCss();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*layout*");
    }
}
