using FluentAssertions;

namespace AdaptiveRemote.Services.CloudAssets;

[TestClass]
public class CloudAssetStoreTests
{
    [TestMethod]
    public void CloudAssetStore_Get_ThrowsOnMissingKey()
    {
        // Arrange
        CloudAssetStore sut = new();

        // Act
        Action act = () => sut.Get<string>("nonexistent");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Asset 'nonexistent' not found in store.*");
    }

    [TestMethod]
    public void CloudAssetStore_Get_ThrowsOnWrongType()
    {
        // Arrange
        CloudAssetStore sut = new();
        sut.Set("test", 42);

        // Act
        Action act = () => sut.Get<string>("test");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Asset 'test' is of type 'Int32', but 'String' was requested.");
    }

    [TestMethod]
    public void CloudAssetStore_SetAndGet_RoundTripsCorrectly()
    {
        // Arrange
        CloudAssetStore sut = new();
        string expectedValue = "test value";

        // Act
        sut.Set("key", expectedValue);
        string actualValue = sut.Get<string>("key");

        // Assert
        actualValue.Should().Be(expectedValue);
    }

    [TestMethod]
    public void CloudAssetStore_Set_OverwritesExistingValue()
    {
        // Arrange
        CloudAssetStore sut = new();
        sut.Set("key", "initial");

        // Act
        sut.Set("key", "updated");
        string actualValue = sut.Get<string>("key");

        // Assert
        actualValue.Should().Be("updated");
    }

    [TestMethod]
    public void CloudAssetStore_Get_SupportsMultipleAssets()
    {
        // Arrange
        CloudAssetStore sut = new();
        sut.Set("asset1", "value1");
        sut.Set("asset2", 123);
        sut.Set("asset3", true);

        // Act & Assert
        sut.Get<string>("asset1").Should().Be("value1");
        sut.Get<int>("asset2").Should().Be(123);
        sut.Get<bool>("asset3").Should().BeTrue();
    }
}
