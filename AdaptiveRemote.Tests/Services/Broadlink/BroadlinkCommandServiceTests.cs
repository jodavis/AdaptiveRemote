using Moq;

namespace AdaptiveRemote.Services.Broadlink;

[TestClass]
public class BroadlinkCommandServiceTests
{
    private readonly Mock<IDeviceLocator> MockLocator = new();
    private readonly Mock<IDeviceConnection.Factory> MockConnectionFactory = new();
    private readonly Mock<IDeviceConnection> MockConnection = new();
    private readonly Mock<IRemoteDefinitionService> MockDefinitionService = new();
    private readonly Mock<ILifecycleActivity> MockInitializeActivity = new();
    private readonly MockLogger<BroadlinkCommandService> MockLogger = new();

    private ILifecycleActivity InitializeActivity => MockInitializeActivity.Object;

    private BroadlinkCommandService CreateSut() => new(MockLocator.Object, MockConnectionFactory.Object, MockDefinitionService.Object, MockLogger);

    [TestInitialize]
    public void SetupMocks()
    {

    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockLocator.Verify();
        MockConnectionFactory.Verify();
        MockConnection.Verify();
        MockDefinitionService.Verify();
    }

    [TestMethod]
    public void BroadlinkCommandService_Name_ReturnsAName()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        // Act
        string name = sut.Name;

        // Assert
        Assert.IsNotNull(name, nameof(name));
    }

    [TestMethod]
    public void BroadlinkCommandService_InitializeAsync_SuccessPath_AuthenticatesAndSetsCommandActions()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        Expect_IDeviceLocator_FindDevice("10.20.30.40:1234", 0x78AB, "AA:BB:CC:DD:EE:FF");

        // Act
        Task resultTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert

    }

    private void Expect_IDeviceLocator_FindDevice(string ip, short deviceType, string mac, bool isLocked = false)
        => MockLocator
            .Setup(x => x.FindDeviceAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(new ScanResponsePacket(ip, deviceType, mac, isLocked))
            .Verifiable(Times.Once);
}
