using Moq;

namespace AdaptiveRemote.Services;

[TestClass]
public class PersistSettingsExtensionsTests
{
    private readonly Mock<IPersistSettings> MockPersistSettings = new();

    [TestMethod]
    public void PersistSettingsExtensions_Set_WithComplexKey_ComputsKey()
    {
        // Arrange
        string[] input = ["foo", "bar", "baz"];
        string expectedName = "foo:bar:baz";
        string expectedValue = "VaLuE";

        MockPersistSettings
            .Setup(x => x.Set(expectedName, expectedValue))
            .Verifiable(Times.Once);

        // Act
        MockPersistSettings.Object.Set(input, expectedValue);

        // Assert
        MockPersistSettings.Verify(x => x.Set(expectedName, expectedValue), Times.Once);
    }

    [TestMethod]
    public void PersistSettingsExtensions_Set_WithIntegerValue_SerializesValue()
    {
        // Arrange
        int input = 1234;
        string expectedName = "foo";
        string expectedValue = "1234";

        MockPersistSettings
            .Setup(x => x.Set(expectedName, expectedValue))
            .Verifiable(Times.Once);

        // Act
        MockPersistSettings.Object.Set(expectedName, input);

        // Assert
        MockPersistSettings.Verify(x => x.Set(expectedName, expectedValue), Times.Once);
    }

    [TestMethod]
    public void PersistSettingsExtensions_Set_WithNullValue_SerializesValue()
    {
        // Arrange
        string expectedName = "foo";
        string expectedValue = "";

        MockPersistSettings
            .Setup(x => x.Set(expectedName, expectedValue))
            .Verifiable(Times.Once);

        // Act
        MockPersistSettings.Object.Set(expectedName, (object?)null);

        // Assert
        MockPersistSettings.Verify(x => x.Set(expectedName, expectedValue), Times.Once);
    }
}
