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
        css.Should().NotBeNullOrEmpty();
    }
}
