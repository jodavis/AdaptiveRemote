using FluentAssertions;

namespace AdaptiveRemote.Services.Layout;

[TestClass]
public class LayoutStylesheetProviderTests
{
    [TestMethod]
    public void LayoutStylesheetProvider_GetCss_ReturnsNonNullContent()
    {
        // Arrange
        LayoutStylesheetProvider sut = new();

        // Act
        string? css = sut.GetCss();

        // Assert
        css.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public void LayoutStylesheetProvider_GetCss_ContainsGridDefinitions()
    {
        // Arrange
        LayoutStylesheetProvider sut = new();

        // Act
        string? css = sut.GetCss();

        // Assert
        css.Should().Contain("#ROOT");
        css.Should().Contain("grid-template-rows");
        css.Should().Contain("grid-template-columns");
    }

    [TestMethod]
    public void LayoutStylesheetProvider_GetCss_CachesResult()
    {
        // Arrange
        LayoutStylesheetProvider sut = new();

        // Act
        string? firstCall = sut.GetCss();
        string? secondCall = sut.GetCss();

        // Assert
        firstCall.Should().BeSameAs(secondCall);
    }
}
