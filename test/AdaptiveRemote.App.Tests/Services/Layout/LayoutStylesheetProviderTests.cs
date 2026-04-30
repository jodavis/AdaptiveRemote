using AdaptiveRemote.Contracts;
using FluentAssertions;

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
        const string expectedCss = "#DPAD { display: grid; grid-template-columns: 3fr 2fr; }";
        CompiledLayout layout = MakeLayout(expectedCss);
        LayoutStylesheetProvider sut = new(layout);

        // Act
        string? css = sut.GetCss();

        // Assert
        css.Should().Be(expectedCss);
    }
}
