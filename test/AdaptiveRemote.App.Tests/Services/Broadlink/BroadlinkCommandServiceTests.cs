using System.Net;
using System.Net.NetworkInformation;
using AdaptiveRemote.Models;
using AdaptiveRemote.Services.ModalMessages;
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
    private readonly Mock<IModalMessageService> MockModalMessageService = new();
    private readonly MockLogger<BroadlinkCommandService> MockLogger = new();

    private ILifecycleActivity InitializeActivity => MockInitializeActivity.Object;

    private BroadlinkCommandService CreateSut(IRDataSettings? irDataSettings = null, BroadlinkSettings? broadlinkSettings = null)
        => new(MockLocator.Object,
               MockConnectionFactory.Object,
               new MockOptionsSnapshot<IRDataSettings>(irDataSettings ?? []),
               new MockOptions<BroadlinkSettings>(broadlinkSettings ?? new()),
               MockPersistSettings.Object,
               MockDefinitionService.Object,
               MockModalMessageService.Object,
               MockLogger);

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
        MockModalMessageService
            .Setup(x => x.ShowMessageAsync(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
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
        MockModalMessageService.Verify();
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

        MockLogger.VerifyMessages(messageLogger =>
        {
            messageLogger.BroadlinkCommandService_SearchingForDevice();
            messageLogger.BroadlinkCommandService_Authenticating(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Authenticated(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Ready();
        });
    }

    [TestMethod]
    public void BroadlinkCommandService_InitializeAsync_WithProgrammedCommand_EnablesCommand()
    {
        // Arrange
        const string commandName = "Power";
        const string base64Data = "AQIDBA==";

        MockDefinitionService
            .Setup(x => x.RemoteRoot)
            .Returns(new LayoutGroup("ROOT", [new IRCommand(commandName)]));

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
        IRCommand command = MockDefinitionService.Object.GetElement<IRCommand>();
        Assert.IsTrue(command.IsEnabled, "Programmed command should be enabled");
        Assert.IsNotNull(command.ExecuteAsync, "Programmed command should have an execute handler");

        MockLogger.VerifyMessages(messageLogger =>
        {
            messageLogger.BroadlinkCommandService_SearchingForDevice();
            messageLogger.BroadlinkCommandService_Authenticating(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Authenticated(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Ready();
        });
    }

    [TestMethod]
    public void BroadlinkCommandService_InitializeAsync_WithUnprogrammedCommand_CommandRemainsDisabled()
    {
        // Arrange
        const string commandName = "Mute";

        MockDefinitionService
            .Setup(x => x.RemoteRoot)
            .Returns(new LayoutGroup("ROOT", [new IRCommand(commandName)]));

        IScopedLifecycle sut = CreateSut(new IRDataSettings()); // empty - no data for "Mute"

        Expect_IDeviceLocator_FindDevice("10.20.30.40:1234", 0x78AB, "AA:BB:CC:DD:EE:FF");
        Expect_ConnectionFactory_Create();
        Expect_Connection_Authenticate();
        Expect_InitializeActivity_Description("Connecting to Broadlink device");

        // Act
        Task resultTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        resultTask.Should().BeComplete(because: "InitializeAsync should complete with unprogrammed command");
        IRCommand command = MockDefinitionService.Object.GetElement<IRCommand>();
        Assert.IsFalse(command.IsEnabled, "Unprogrammed command should remain disabled");

        MockLogger.VerifyMessages(messageLogger =>
        {
            messageLogger.BroadlinkCommandService_SearchingForDevice();
            messageLogger.BroadlinkCommandService_Authenticating(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Authenticated(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Ready();
        });
    }

    [TestMethod]
    public void BroadlinkCommandService_InitializeAsync_SetsProgramAsyncOnAllCommands()
    {
        // Arrange
        const string commandName = "Power";
        IRCommand command = SetupAndInitializeWithCommand(commandName);

        // Assert
        Assert.IsNotNull(command.ProgramAsync, "All IR commands should have a ProgramAsync handler");

        MockLogger.VerifyMessages(messageLogger =>
        {
            messageLogger.BroadlinkCommandService_SearchingForDevice();
            messageLogger.BroadlinkCommandService_Authenticating(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Authenticated(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Ready();
        });
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramAsync_HappyPath_EntersLearningAndStoresData()
    {
        // Arrange
        const string commandName = "Power";
        byte[] learnedData = [0x01, 0x02, 0x03, 0x04];
        string expectedBase64 = Convert.ToBase64String(learnedData);
        string expectedSettingKey = $"IRData:{commandName}";

        BroadlinkSettings settings = new() { LearnPollInterval = 0 };
        IRCommand command = SetupAndInitializeWithCommand(commandName, settings: settings);

        Expect_Connection_EnterLearningMode();
        Expect_Connection_CheckLearnedData(null, learnedData);
        Expect_PersistSettings_Set(expectedSettingKey, expectedBase64);
        Expect_ModalMessageService_ShowMessage(Phrases.Broadlink_ProgrammingCommand(commandName));

        // Act
        Task programTask = command.ProgramAsync!(default);

        // Assert
        programTask.Should().BeComplete(because: "ProgramAsync should complete after learning data is received");

        MockLogger.VerifyMessages(messageLogger =>
        {
            messageLogger.BroadlinkCommandService_SearchingForDevice();
            messageLogger.BroadlinkCommandService_Authenticating(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Authenticated(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Ready();
            messageLogger.CommandService_Programming(command);
            messageLogger.BroadlinkCommandService_EnteringLearningMode(command);
            messageLogger.BroadlinkCommandService_PollingForLearnedData(command);
            messageLogger.BroadlinkCommandService_PollingForLearnedData(command);
            messageLogger.BroadlinkCommandService_LearnedDataReceived(command, learnedData.Length);
            messageLogger.CommandService_Programmed(command);
        });
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramAsync_HappyPath_UpdatesExecuteAsyncOnCommand()
    {
        // Arrange
        const string commandName = "Volume Up";
        byte[] learnedData = [0x11, 0x22, 0x33, 0x44];
        string expectedBase64 = Convert.ToBase64String(learnedData);

        // Start with no IR data so the command starts disabled
        BroadlinkSettings settings = new() { LearnPollInterval = 0 };
        IRCommand command = SetupAndInitializeWithCommand(commandName, irData: new IRDataSettings(), settings: settings);
        Assert.IsFalse(command.IsEnabled, "Command should start disabled without IR data");

        Expect_Connection_EnterLearningMode();
        Expect_Connection_CheckLearnedData(null, learnedData);
        Expect_PersistSettings_Set($"IRData:{commandName}", expectedBase64);
        Expect_ModalMessageService_ShowMessage(Phrases.Broadlink_ProgrammingCommand(commandName));

        // Act
        Task programTask = command.ProgramAsync!(default);

        // Assert
        programTask.Should().BeComplete(because: "ProgramAsync should complete after data is received");
        Assert.IsTrue(command.IsEnabled, "Command should be enabled after programming");
        Assert.IsNotNull(command.ExecuteAsync, "Command should have an execute handler after programming");

        // Verify the new execute handler invokes SendDataAsync with the learned data
        MockConnection
            .Setup(x => x.SendDataAsync(learnedData, It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior()
            .Verifiable(Times.Once);

        Task executeTask = command.ExecuteAsync!(default);
        executeTask.Should().BeComplete(because: "ExecuteAsync should succeed with the newly learned data");
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramAsync_CancelledDuringEnterLearning_TaskCancels()
    {
        // Arrange
        const string commandName = "Mute";
        BroadlinkSettings settings = new() { LearnPollInterval = 0 };
        IRCommand command = SetupAndInitializeWithCommand(commandName, settings: settings);

        CancellationTokenSource cts = new();
        MockConnection
            .Setup(x => x.EnterLearningModeAsync(It.IsAny<CancellationToken>()))
            .WithExpectedCancellation(throwWhenCancelled: true);

        Expect_ModalMessageService_ShowMessage(Phrases.Broadlink_ProgrammingCommand(commandName));

        // Act
        Task programTask = command.ProgramAsync!(cts.Token);
        cts.Cancel();

        // Assert
        programTask.Should().BeCanceledWithin(TimeSpan.FromSeconds(5),
            because: "ProgramAsync should cancel when the cancellation token is cancelled");

        MockLogger.VerifyMessages(messageLogger =>
        {
            messageLogger.BroadlinkCommandService_SearchingForDevice();
            messageLogger.BroadlinkCommandService_Authenticating(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Authenticated(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Ready();
            messageLogger.CommandService_Programming(command);
            messageLogger.BroadlinkCommandService_EnteringLearningMode(command);
            messageLogger.CommandService_ProgramCancelled(command);
        });
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramAsync_CancelledDuringPolling_TaskCancels()
    {
        // Arrange
        const string commandName = "Mute";
        BroadlinkSettings settings = new() { LearnPollInterval = 0 };
        IRCommand command = SetupAndInitializeWithCommand(commandName, settings: settings);

        CancellationTokenSource cts = new();

        Expect_Connection_EnterLearningMode();
        MockConnection
            .Setup(x => x.CheckLearnedDataAsync(It.IsAny<CancellationToken>()))
            .WithExpectedCancellation(throwWhenCancelled: true);

        Expect_ModalMessageService_ShowMessage(Phrases.Broadlink_ProgrammingCommand(commandName));

        // Act
        Task programTask = command.ProgramAsync!(cts.Token);
        cts.Cancel();

        // Assert
        programTask.Should().BeCanceledWithin(TimeSpan.FromSeconds(5),
            because: "ProgramAsync should cancel when cancelled during polling");

        MockLogger.VerifyMessages(messageLogger =>
        {
            messageLogger.BroadlinkCommandService_SearchingForDevice();
            messageLogger.BroadlinkCommandService_Authenticating(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Authenticated(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Ready();
            messageLogger.CommandService_Programming(command);
            messageLogger.BroadlinkCommandService_EnteringLearningMode(command);
            messageLogger.BroadlinkCommandService_PollingForLearnedData(command);
            messageLogger.CommandService_ProgramCancelled(command);
        });
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramAsync_DeviceErrorDuringEnterLearning_ThrowsBroadlinkException()
    {
        // Arrange
        const string commandName = "Power";
        BroadlinkSettings settings = new() { LearnPollInterval = 0 };
        IRCommand command = SetupAndInitializeWithCommand(commandName, settings: settings);

        BroadlinkException expectedException = new("Device is offline");
        MockConnection
            .Setup(x => x.EnterLearningModeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException)
            .Verifiable(Times.Once);

        Expect_ModalMessageService_ShowMessage(Phrases.Broadlink_ProgrammingCommand(commandName));

        // Act
        Task programTask = command.ProgramAsync!(default);

        // Assert
        programTask.Should().BeFaultedWith(expectedException,
            because: "ProgramAsync should propagate device errors during enter learning");

        MockLogger.VerifyMessages(messageLogger =>
        {
            messageLogger.BroadlinkCommandService_SearchingForDevice();
            messageLogger.BroadlinkCommandService_Authenticating(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Authenticated(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Ready();
            messageLogger.CommandService_Programming(command);
            messageLogger.BroadlinkCommandService_EnteringLearningMode(command);
            messageLogger.CommandService_ProgramError(command, expectedException);
        });
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramAsync_DeviceErrorDuringPolling_ThrowsBroadlinkException()
    {
        // Arrange
        const string commandName = "Power";
        BroadlinkSettings settings = new() { LearnPollInterval = 0 };
        IRCommand command = SetupAndInitializeWithCommand(commandName, settings: settings);

        Expect_Connection_EnterLearningMode();
        Expect_ModalMessageService_ShowMessage(Phrases.Broadlink_ProgrammingCommand(commandName));

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

        MockLogger.VerifyMessages(messageLogger =>
        {
            messageLogger.BroadlinkCommandService_SearchingForDevice();
            messageLogger.BroadlinkCommandService_Authenticating(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Authenticated(IPEndPoint.Parse("10.20.30.40:1234"));
            messageLogger.BroadlinkCommandService_Ready();
            messageLogger.CommandService_Programming(command);
            messageLogger.BroadlinkCommandService_EnteringLearningMode(command);
            messageLogger.BroadlinkCommandService_PollingForLearnedData(command);
            messageLogger.CommandService_ProgramError(command, expectedException);
        });
    }

    private IRCommand SetupAndInitializeWithCommand(string commandName, IRDataSettings? irData = null, BroadlinkSettings? settings = null)
    {
        MockDefinitionService
            .Setup(x => x.RemoteRoot)
            .Returns(new LayoutGroup("ROOT", [new IRCommand(commandName)]));

        IScopedLifecycle sut = CreateSut(irData, settings);

        Expect_IDeviceLocator_FindDevice("10.20.30.40:1234", 0x78AB, "AA:BB:CC:DD:EE:FF");
        Expect_ConnectionFactory_Create();
        Expect_Connection_Authenticate();
        Expect_InitializeActivity_Description("Connecting to Broadlink device");

        sut.InitializeAsync(InitializeActivity, default);
        return MockDefinitionService.Object.GetElement<IRCommand>();
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

    private void Expect_Connection_EnterLearningMode()
        => MockConnection
            .Setup(x => x.EnterLearningModeAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior()
            .Verifiable(Times.Once);

    private void Expect_ModalMessageService_ShowMessage(string expectedMessage)
        => MockModalMessageService
            .Setup(x => x.ShowMessageAsync(expectedMessage, It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(delegate(string message, Func<CancellationToken, Task> action, bool keepAlive, CancellationToken ct)
            {
                Assert.AreEqual(expectedMessage, message, "Modal message should have the expected text");
                return action(ct);
            })
            .Verifiable(Times.Once);

    private void Expect_Connection_CheckLearnedData(params byte[]?[] returnSequence)
    {
        Moq.Language.ISetupSequentialResult<Task<byte[]?>> setup = MockConnection
            .SetupSequence(x => x.CheckLearnedDataAsync(It.IsAny<CancellationToken>()));
        foreach (byte[]? result in returnSequence)
        {
            setup = setup.ReturnsAsync(result);
        }
    }

    private void Expect_PersistSettings_Set(string key, string value)
        => MockPersistSettings
            .Setup(x => x.Set(key, value))
            .Verifiable(Times.Once);

    private void Expect_InitializeActivity_Description(string expectedDescription)
        => MockInitializeActivity
            .SetupSet(x => x.Description = It.IsAny<string>())
            .Callback(delegate (string description) { description.Should().Be(expectedDescription); });
}

