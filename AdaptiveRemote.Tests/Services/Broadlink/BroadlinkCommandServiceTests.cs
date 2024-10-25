using System.Net;
using System.Net.NetworkInformation;
using FluentAssertions;
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
        MockInitializeActivity
            .SetupSet(x => x.Description = It.IsAny<string>())
            .Verifiable(Times.Never);
        MockInitializeActivity
            .Setup(x => x.SetFatalError(It.IsAny<Exception>()))
            .Callback(delegate (Exception ex) { Assert.Fail("SetFatalError was called on the activity: {0}", ex); });
        MockInitializeActivity
            .Setup(x => x.Dispose())
            .Verifiable(Times.Never);
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockLocator.Verify();
        MockConnectionFactory.Verify();
        MockConnection.Verify();
        MockDefinitionService.Verify();
        MockInitializeActivity.Verify();
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
        Expect_ConnectionFactory_Create();
        Expect_Connection_AuthenticateAsync();
        Expect_InitializeActivity_Description("Connecting to Broadlink device");

        // Act
        Task resultTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        resultTask.Should().BeComplete(because: "InitializeAsync should complete after command service is initialized");
    }

    private void Expect_IDeviceLocator_FindDevice(string ip, short deviceType, string mac, bool isLocked = false)
        => MockLocator
            .Setup(x => x.FindDeviceAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(new ScanResponsePacket(ip, deviceType, mac, isLocked))
            .Verifiable(Times.Once);
    private void Expect_ConnectionFactory_Create()
        => MockConnectionFactory
            .Setup(x => x.Create(It.IsAny<IPEndPoint>(), It.IsAny<PhysicalAddress>(), It.IsAny<short>()))
            .Returns(MockConnection.Object)
            .Verifiable(Times.Once);
    private void Expect_Connection_AuthenticateAsync()
        => MockConnection
            .Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(true)
            .Verifiable(Times.Once);
    private void Expect_InitializeActivity_Description(string expectedDescription)
        => MockInitializeActivity
            .SetupSet(x => x.Description = It.IsAny<string>())
            .Callback(delegate (string description) { description.Should().Be(expectedDescription); });
}
