using AdaptiveRemote.Models;
using AdaptiveRemote.Services.CloudAssets;
using FluentAssertions;
using Moq;

namespace AdaptiveRemote.Services.Layout;

[TestClass]
public class RemoteLayoutDefinitionServiceTests
{
    private readonly Mock<ICloudAssetStore> _mockStore = new();

    [TestCleanup]
    public void VerifyMocks()
    {
        _mockStore.Verify();
    }

    [TestMethod]
    public void RemoteLayoutDefinitionService_RemoteRoot_ReturnsStoreContents()
    {
        // Arrange
        LayoutGroup expectedLayout = new("ROOT", []);
        _mockStore.Setup(s => s.Get<LayoutGroup>("layout"))
            .Returns(expectedLayout)
            .Verifiable();

        RemoteLayoutDefinitionService sut = new(_mockStore.Object);

        // Act
        RemoteLayoutElement actualRoot = sut.RemoteRoot;

        // Assert
        actualRoot.Should().BeSameAs(expectedLayout);
    }

    [TestMethod]
    public void RemoteLayoutDefinitionService_RemoteRoot_EmptyStoreThrowsDescriptiveError()
    {
        // Arrange
        _mockStore.Setup(s => s.Get<LayoutGroup>("layout"))
            .Throws(new InvalidOperationException("Asset 'layout' not found in store."))
            .Verifiable();

        RemoteLayoutDefinitionService sut = new(_mockStore.Object);

        // Act
        Action act = () => _ = sut.RemoteRoot;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Failed to load layout from CloudAssetStore.*");
    }

    [TestMethod]
    public void RemoteLayoutDefinitionService_RemoteRoot_WrongTypeThrowsDescriptiveError()
    {
        // Arrange
        _mockStore.Setup(s => s.Get<LayoutGroup>("layout"))
            .Throws(new InvalidOperationException("Asset 'layout' is of type 'String', but 'LayoutGroup' was requested."))
            .Verifiable();

        RemoteLayoutDefinitionService sut = new(_mockStore.Object);

        // Act
        Action act = () => _ = sut.RemoteRoot;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Failed to load layout from CloudAssetStore.*");
    }

    [TestMethod]
    public void RemoteLayoutDefinitionService_RemoteRoot_ReturnsComplexLayout()
    {
        // Arrange
        LayoutGroup expectedLayout = new("ROOT",
        [
            new LayoutGroup("GROUP1",
            [
                new TiVoCommand("Up"),
                new TiVoCommand("Down"),
            ]),
            new LayoutGroup("GROUP2",
            [
                new IRCommand("Power"),
            ])
        ]);

        _mockStore.Setup(s => s.Get<LayoutGroup>("layout"))
            .Returns(expectedLayout)
            .Verifiable();

        RemoteLayoutDefinitionService sut = new(_mockStore.Object);

        // Act
        RemoteLayoutElement actualRoot = sut.RemoteRoot;

        // Assert
        actualRoot.Should().BeSameAs(expectedLayout);
    }
}
