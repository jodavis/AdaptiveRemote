using System.Net;
using System.Net.NetworkInformation;
using AdaptiveRemote.Models;
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
    private readonly Mock<IPersistSettings> MockPersistSettings = new();
    private readonly MockOptions<BroadlinkSettings> MockSettings = new(new());
    private readonly Mock<ILifecycleActivity> MockInitializeActivity = new();
    private readonly MockLogger<BroadlinkCommandService> MockLogger = new();

    private ILifecycleActivity InitializeActivity => MockInitializeActivity.Object;

    private BroadlinkCommandService CreateSut() => new(MockLocator.Object, MockConnectionFactory.Object, MockDefinitionService.Object, MockPersistSettings.Object, MockSettings, MockLogger);

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
        Expect_Connection_Authenticate();
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
    private void Expect_Connection_Authenticate()
        => MockConnection
            .Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(true)
            .Verifiable(Times.Once);
    private void Expect_InitializeActivity_Description(string expectedDescription)
        => MockInitializeActivity
            .SetupSet(x => x.Description = It.IsAny<string>())
            .Callback(delegate (string description) { description.Should().Be(expectedDescription); });

    private void Expect_StandardInitialization(string ip = "10.20.30.40:1234", short deviceType = 0x78AB, string mac = "AA:BB:CC:DD:EE:FF")
    {
        Expect_IDeviceLocator_FindDevice(ip, deviceType, mac);
        Expect_ConnectionFactory_Create();
        Expect_Connection_Authenticate();
        Expect_InitializeActivity_Description("Connecting to Broadlink device");
    }

    private IEnumerable<IRCommand> SetupCommandsInDefinitionService(params IRCommand[] commands)
    {
        LayoutGroup layout = new("ROOT", commands);
        MockDefinitionService
            .SetupGet(x => x.RemoteRoot)
            .Returns(layout);
        return commands;
    }

    [TestMethod]
    public void BroadlinkCommandService_InitializeAsync_SetsProgramAsyncOnCommands()
    {
        // Arrange
        IRCommand command = new("Power", "AAAA");
        SetupCommandsInDefinitionService(command);
        IScopedLifecycle sut = CreateSut();

        Expect_StandardInitialization();

        // Act
        Task resultTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        resultTask.Should().BeComplete(because: "InitializeAsync should complete");
        Assert.IsNotNull(command.ProgramAsync, nameof(command.ProgramAsync));
    }

    [TestMethod]
    public void BroadlinkCommandService_InitializeAsync_WhenProgrammedDataExists_UsesItForCommand()
    {
        // Arrange
        byte[] hardCodedData = [0x01, 0x02];
        byte[] programmedData = [0x55, 0x66, 0x77];
        IRCommand command = new("Power", Convert.ToBase64String(hardCodedData));
        SetupCommandsInDefinitionService(command);
        IScopedLifecycle sut = CreateSut();

        MockPersistSettings
            .Setup(x => x.GetAsync("IRData:Power"))
            .ReturnsAsync(Convert.ToBase64String(programmedData))
            .Verifiable(Times.Once);

        Expect_StandardInitialization();
        MockConnection
            .Setup(x => x.SendDataAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .WithArgumentValidation("data", delegate (byte[] data)
            {
                MemoryAssert.AreEqual(programmedData, data, "SendDataAsync should use programmed IR data");
            })
            .WithStandardTaskBehavior()
            .Verifiable(Times.Once);

        // Act
        Task resultTask = sut.InitializeAsync(InitializeActivity, default);
        resultTask.Should().BeComplete();

        Task executeTask = command.ExecuteAsync!(default);

        // Assert
        executeTask.Should().BeComplete(because: "ExecuteAsync should complete using the programmed IR data");
    }

    [TestMethod]
    public void BroadlinkCommandService_InitializeAsync_WhenNoProgrammedData_UsesHardCodedData()
    {
        // Arrange
        byte[] hardCodedData = [0x01, 0x02];
        IRCommand command = new("Power", Convert.ToBase64String(hardCodedData));
        SetupCommandsInDefinitionService(command);
        IScopedLifecycle sut = CreateSut();

        MockPersistSettings
            .Setup(x => x.GetAsync("IRData:Power"))
            .ReturnsAsync((string?)null)
            .Verifiable(Times.Once);

        Expect_StandardInitialization();
        MockConnection
            .Setup(x => x.SendDataAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .WithArgumentValidation("data", delegate (byte[] data)
            {
                MemoryAssert.AreEqual(hardCodedData, data, "SendDataAsync should use hard-coded IR data");
            })
            .WithStandardTaskBehavior()
            .Verifiable(Times.Once);

        // Act
        Task resultTask = sut.InitializeAsync(InitializeActivity, default);
        resultTask.Should().BeComplete();

        Task executeTask = command.ExecuteAsync!(default);

        // Assert
        executeTask.Should().BeComplete(because: "ExecuteAsync should complete using the hard-coded IR data");
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramDelegate_EntersLearningModeAndSavesData()
    {
        // Arrange
        byte[] learnedIRData = [0xAA, 0xBB, 0xCC];
        IRCommand command = new("Power", "AAAA");
        SetupCommandsInDefinitionService(command);
        IScopedLifecycle sut = CreateSut();

        Expect_StandardInitialization();

        MockConnection
            .Setup(x => x.EnterLearningModeAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior()
            .Verifiable(Times.Once);
        MockConnection
            .Setup(x => x.CheckLearnedDataAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(Task.FromResult<byte[]?>(learnedIRData))
            .Verifiable(Times.Once);

        string? savedKey = null;
        string? savedValue = null;
        MockPersistSettings
            .Setup(x => x.Set(It.IsAny<string>(), It.IsAny<string>()))
            .Callback(delegate (string key, string value)
            {
                savedKey = key;
                savedValue = value;
            })
            .Verifiable(Times.Once);

        // Act
        sut.InitializeAsync(InitializeActivity, default).Wait();
        Task programTask = command.ProgramAsync!(default);

        // Assert
        programTask.Should().BeComplete(because: "ProgramDelegate should complete after capturing IR data");
        Assert.AreEqual("IRData:Power", savedKey, nameof(savedKey));
        Assert.AreEqual(Convert.ToBase64String(learnedIRData), savedValue, nameof(savedValue));
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramDelegate_PollsUntilDataAvailable()
    {
        // Arrange
        byte[] learnedIRData = [0x11, 0x22, 0x33];
        IRCommand command = new("Volume", "AAAA");
        SetupCommandsInDefinitionService(command);
        IScopedLifecycle sut = CreateSut();

        Expect_StandardInitialization();

        MockConnection
            .Setup(x => x.EnterLearningModeAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior()
            .Verifiable(Times.Once);

        int pollCount = 0;
        MockConnection
            .Setup(x => x.CheckLearnedDataAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                pollCount++;
                return Task.FromResult<byte[]?>(pollCount >= 3 ? learnedIRData : null);
            })
            .Verifiable(Times.Exactly(3));

        MockPersistSettings
            .Setup(x => x.Set(It.IsAny<string>(), It.IsAny<string>()))
            .Verifiable(Times.Once);

        // Act
        sut.InitializeAsync(InitializeActivity, default).Wait();
        Task programTask = command.ProgramAsync!(default);

        // Assert
        programTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(5),
            because: "ProgramDelegate should eventually succeed after polling");
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramDelegate_WhenTimeout_ThrowsTimeoutException()
    {
        // Arrange
        IRCommand command = new("Power", "AAAA");
        SetupCommandsInDefinitionService(command);
        IScopedLifecycle sut = new BroadlinkCommandService(
            MockLocator.Object, MockConnectionFactory.Object, MockDefinitionService.Object,
            MockPersistSettings.Object, new MockOptions<BroadlinkSettings>(new() { LearningTimeout = 0 }), MockLogger);

        Expect_StandardInitialization();

        MockConnection
            .Setup(x => x.EnterLearningModeAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior()
            .Verifiable(Times.Once);
        MockConnection
            .Setup(x => x.CheckLearnedDataAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult<byte[]?>(null);
            });

        // Act
        sut.InitializeAsync(InitializeActivity, default).Wait();
        Task programTask = command.ProgramAsync!(default);

        // Assert
        programTask.Should().BeFaultedWith(Errors.Broadlink_LearningTimeout(),
            because: "ProgramDelegate should throw TimeoutException when the learning timeout expires");
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramDelegate_WhenCancelled_PropagatesCancellation()
    {
        // Arrange
        IRCommand command = new("Power", "AAAA");
        SetupCommandsInDefinitionService(command);
        IScopedLifecycle sut = CreateSut();

        Expect_StandardInitialization();

        CancellationTokenSource cts = new();

        MockConnection
            .Setup(x => x.EnterLearningModeAsync(It.IsAny<CancellationToken>()))
            .WithExpectedCancellation(throwWhenCancelled: true);

        // Act
        sut.InitializeAsync(InitializeActivity, default).Wait();
        Task programTask = command.ProgramAsync!(cts.Token);
        cts.Cancel();

        // Assert
        programTask.Should().BeCanceledWithin(TimeSpan.FromMilliseconds(500),
            because: "ProgramDelegate should propagate cancellation");
    }

    [TestMethod]
    public void BroadlinkCommandService_ProgramDelegate_UpdatesExecuteAsyncToUseProgrammedData()
    {
        // Arrange
        byte[] originalData = [0x01, 0x02];
        byte[] learnedIRData = [0xAA, 0xBB, 0xCC];
        IRCommand command = new("Power", Convert.ToBase64String(originalData));
        SetupCommandsInDefinitionService(command);
        IScopedLifecycle sut = CreateSut();

        Expect_StandardInitialization();

        MockConnection
            .Setup(x => x.EnterLearningModeAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior()
            .Verifiable(Times.Once);
        MockConnection
            .Setup(x => x.CheckLearnedDataAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(Task.FromResult<byte[]?>(learnedIRData))
            .Verifiable(Times.Once);
        MockPersistSettings
            .Setup(x => x.Set(It.IsAny<string>(), It.IsAny<string>()))
            .Verifiable(Times.Once);
        MockConnection
            .Setup(x => x.SendDataAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .WithArgumentValidation("data", delegate (byte[] data)
            {
                MemoryAssert.AreEqual(learnedIRData, data, "SendDataAsync should use newly learned IR data");
            })
            .WithStandardTaskBehavior()
            .Verifiable(Times.Once);

        // Act
        sut.InitializeAsync(InitializeActivity, default).Wait();
        command.ProgramAsync!(default).Wait();
        Task executeTask = command.ExecuteAsync!(default);

        // Assert
        executeTask.Should().BeComplete(because: "ExecuteAsync should use the newly programmed IR data");
    }
}
