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
    private readonly Mock<IPersistSettings> MockPersistSettings = new();
    private readonly MockLogger<BroadlinkCommandService> MockLogger = new();

    private ILifecycleActivity InitializeActivity => MockInitializeActivity.Object;

    private BroadlinkCommandService CreateSut(IRDataSettings? irDataSettings = null, BroadlinkSettings? broadlinkSettings = null)
        => new(MockLocator.Object, MockConnectionFactory.Object, new MockOptionsSnapshot<IRDataSettings>(irDataSettings ?? new()), new MockOptions<BroadlinkSettings>(broadlinkSettings ?? new()), MockPersistSettings.Object, MockDefinitionService.Object, MockLogger);

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
        MockPersistSettings.Verify();
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
        Expect_Connection_Authenticate();
        Expect_InitializeActivity_Description("Connecting to Broadlink device");

        // Act
        Task resultTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        resultTask.Should().BeComplete(because: "InitializeAsync should complete after command service is initialized");
    }

    [TestMethod]
    public void BroadlinkCommandService_InitializeAsync_WithProgrammedCommand_EnablesCommand()
    {
        // Arrange
        const string commandName = "Power";
        const string base64Data = "AQIDBA==";

        MockDefinitionService
            .Setup(x => x.RemoteRoot)
            .Returns(new AdaptiveRemote.Models.LayoutGroup("ROOT", [new AdaptiveRemote.Models.IRCommand(commandName)]));

        IRDataSettings irData = new() { [commandName] = base64Data };
        IScopedLifecycle sut = CreateSut(irData);

        Expect_IDeviceLocator_FindDevice("10.20.30.40:1234", 0x78AB, "AA:BB:CC:DD:EE:FF");
        Expect_ConnectionFactory_Create();
        Expect_Connection_Authenticate();
        Expect_InitializeActivity_Description("Connecting to Broadlink device");

        // Act
        Task resultTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        resultTask.Should().BeComplete(because: "InitializeAsync should complete with programmed command");
        AdaptiveRemote.Models.IRCommand command = MockDefinitionService.Object.GetElement<AdaptiveRemote.Models.IRCommand>();
        Assert.IsTrue(command.IsEnabled, "Programmed command should be enabled");
        Assert.IsNotNull(command.ExecuteAsync, "Programmed command should have an execute handler");
    }

    [TestMethod]
    public void BroadlinkCommandService_InitializeAsync_WithUnprogrammedCommand_CommandRemainsDisabled()
    {
        // Arrange
        const string commandName = "Mute";

        MockDefinitionService
            .Setup(x => x.RemoteRoot)
            .Returns(new AdaptiveRemote.Models.LayoutGroup("ROOT", [new AdaptiveRemote.Models.IRCommand(commandName)]));

        IScopedLifecycle sut = CreateSut(new IRDataSettings()); // empty - no data for "Mute"

        Expect_IDeviceLocator_FindDevice("10.20.30.40:1234", 0x78AB, "AA:BB:CC:DD:EE:FF");
        Expect_ConnectionFactory_Create();
        Expect_Connection_Authenticate();
        Expect_InitializeActivity_Description("Connecting to Broadlink device");

        // Act
        Task resultTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        resultTask.Should().BeComplete(because: "InitializeAsync should complete with unprogrammed command");
        AdaptiveRemote.Models.IRCommand command = MockDefinitionService.Object.GetElement<AdaptiveRemote.Models.IRCommand>();
        Assert.IsFalse(command.IsEnabled, "Unprogrammed command should remain disabled");
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
    private void Expect_Connection_Authenticate()
        => MockConnection
            .Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(true)
            .Verifiable(Times.Once);
    private void Expect_InitializeActivity_Description(string expectedDescription)
        => MockInitializeActivity
            .SetupSet(x => x.Description = It.IsAny<string>())
            .Callback(delegate (string description) { description.Should().Be(expectedDescription); });

    private AdaptiveRemote.Models.IRCommand SetupAndInitializeWithCommand(string commandName, IRDataSettings? irData = null, BroadlinkSettings? settings = null)
    {
        MockDefinitionService
            .Setup(x => x.RemoteRoot)
            .Returns(new AdaptiveRemote.Models.LayoutGroup("ROOT", [new AdaptiveRemote.Models.IRCommand(commandName)]));

        IScopedLifecycle sut = CreateSut(irData, settings);

        Expect_IDeviceLocator_FindDevice("10.20.30.40:1234", 0x78AB, "AA:BB:CC:DD:EE:FF");
        Expect_ConnectionFactory_Create();
        Expect_Connection_Authenticate();
        Expect_InitializeActivity_Description("Connecting to Broadlink device");

        sut.InitializeAsync(InitializeActivity, default);
        return MockDefinitionService.Object.GetElement<AdaptiveRemote.Models.IRCommand>();
    }

    [TestMethod]
    public void BroadlinkCommandService_InitializeAsync_SetsProgramAsyncOnAllCommands()
    {
        // Arrange
        const string commandName = "Power";
        AdaptiveRemote.Models.IRCommand command = SetupAndInitializeWithCommand(commandName);

        // Assert
        Assert.IsNotNull(command.ProgramAsync, "All IR commands should have a ProgramAsync handler");
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramAsync_HappyPath_EntersLearningAndStoresData()
    {
        // Arrange
        const string commandName = "Power";
        byte[] learnedData = [0x01, 0x02, 0x03, 0x04];
        string expectedBase64 = Convert.ToBase64String(learnedData);
        string expectedSettingKey = $"IRData:{commandName}";

        BroadlinkSettings settings = new() { LearnTimeout = 30, LearnPollInterval = 0 };
        AdaptiveRemote.Models.IRCommand command = SetupAndInitializeWithCommand(commandName, settings: settings);

        MockConnection
            .Setup(x => x.EnterLearningModeAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior()
            .Verifiable(Times.Once);

        MockConnection
            .SetupSequence(x => x.CheckLearnedDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null)
            .ReturnsAsync(learnedData);

        MockPersistSettings
            .Setup(x => x.Set(expectedSettingKey, expectedBase64))
            .Verifiable(Times.Once);

        // Act
        Task programTask = command.ProgramAsync!(default);

        // Assert
        programTask.Should().BeComplete(because: "ProgramAsync should complete after learning data is received");
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramAsync_TimesOut_ThrowsTimeoutException()
    {
        // Arrange
        const string commandName = "Volume Up";

        // Use zero timeout so the polling loop never executes
        BroadlinkSettings settings = new() { LearnTimeout = 0, LearnPollInterval = 0 };
        AdaptiveRemote.Models.IRCommand command = SetupAndInitializeWithCommand(commandName, settings: settings);

        MockConnection
            .Setup(x => x.EnterLearningModeAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior()
            .Verifiable(Times.Once);

        // Act
        Task programTask = command.ProgramAsync!(default);

        // Assert
        programTask.Should().BeFaultedWith(
            Errors.Broadlink_LearningTimedOut(TimeSpan.FromSeconds(0)),
            because: "ProgramAsync should throw TimeoutException when learning times out");
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramAsync_CancelledDuringEnterLearning_TaskCancels()
    {
        // Arrange
        const string commandName = "Mute";
        BroadlinkSettings settings = new() { LearnTimeout = 30, LearnPollInterval = 0 };
        AdaptiveRemote.Models.IRCommand command = SetupAndInitializeWithCommand(commandName, settings: settings);

        CancellationTokenSource cts = new();
        CancellationToken cancellationToken = MockConnection
            .Setup(x => x.EnterLearningModeAsync(It.IsAny<CancellationToken>()))
            .WithExpectedCancellation(throwWhenCancelled: true);

        // Act
        Task programTask = command.ProgramAsync!(cts.Token);
        cts.Cancel();

        // Assert
        programTask.Should().BeCanceledWithin(TimeSpan.FromSeconds(5),
            because: "ProgramAsync should cancel when the cancellation token is cancelled");
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramAsync_CancelledDuringPolling_TaskCancels()
    {
        // Arrange
        const string commandName = "Mute";
        BroadlinkSettings settings = new() { LearnTimeout = 30, LearnPollInterval = 0 };
        AdaptiveRemote.Models.IRCommand command = SetupAndInitializeWithCommand(commandName, settings: settings);

        CancellationTokenSource cts = new();

        MockConnection
            .Setup(x => x.EnterLearningModeAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior()
            .Verifiable(Times.Once);

        CancellationToken cancellationToken = MockConnection
            .Setup(x => x.CheckLearnedDataAsync(It.IsAny<CancellationToken>()))
            .WithExpectedCancellation(throwWhenCancelled: true);

        // Act
        Task programTask = command.ProgramAsync!(cts.Token);
        cts.Cancel();

        // Assert
        programTask.Should().BeCanceledWithin(TimeSpan.FromSeconds(5),
            because: "ProgramAsync should cancel when cancelled during polling");
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramAsync_DeviceErrorDuringEnterLearning_ThrowsBroadlinkException()
    {
        // Arrange
        const string commandName = "Power";
        BroadlinkSettings settings = new() { LearnTimeout = 30, LearnPollInterval = 0 };
        AdaptiveRemote.Models.IRCommand command = SetupAndInitializeWithCommand(commandName, settings: settings);

        BroadlinkException expectedException = new("Device is offline");
        MockConnection
            .Setup(x => x.EnterLearningModeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException)
            .Verifiable(Times.Once);

        // Act
        Task programTask = command.ProgramAsync!(default);

        // Assert
        programTask.Should().BeFaultedWith(expectedException,
            because: "ProgramAsync should propagate device errors during enter learning");
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramAsync_DeviceErrorDuringPolling_ThrowsBroadlinkException()
    {
        // Arrange
        const string commandName = "Power";
        BroadlinkSettings settings = new() { LearnTimeout = 30, LearnPollInterval = 0 };
        AdaptiveRemote.Models.IRCommand command = SetupAndInitializeWithCommand(commandName, settings: settings);

        MockConnection
            .Setup(x => x.EnterLearningModeAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior()
            .Verifiable(Times.Once);

        BroadlinkException expectedException = new("Read error");
        MockConnection
            .Setup(x => x.CheckLearnedDataAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException)
            .Verifiable(Times.Once);

        // Act
        Task programTask = command.ProgramAsync!(default);

        // Assert
        programTask.Should().BeFaultedWith(expectedException,
            because: "ProgramAsync should propagate device errors during polling");
    }
}
