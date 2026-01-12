using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AdaptiveRemote.Services;

[TestClass]
public class CommandServiceBaseTests
{
    private readonly Mock<IRemoteDefinitionService> MockRemoteDefinition = new();
    private readonly Mock<ILifecycleActivity> MockInitializeActivity = new() { Name = nameof(MockInitializeActivity) };
    private readonly Mock<ILifecycleActivity> MockCleanupActivity = new() { Name = nameof(MockCleanupActivity) };
    private readonly MockLogger<MockCommandService> MockLogger = new();

    private ILifecycleActivity InitializeActivity => MockInitializeActivity.Object;
    private ILifecycleActivity CleanupActivity => MockCleanupActivity.Object;

    private readonly LayoutGroup RemoteDefinition = new LayoutGroup("ROOT",
        [
            new MockCommand("Mock1") { IsEnabled = true, IsActive = true },
            new OtherCommand("Other1") { IsEnabled = true, IsActive = true },
            new MockCommand("Mock2") { IsEnabled = true, IsActive = true },
            new OtherCommand("Other2") { IsEnabled = true, IsActive = true },
            new OtherCommand("Other3") { IsEnabled = true, IsActive = true },
            new MockCommand("Mock3") { IsEnabled = true, IsActive = true },
        ]);

    private MockCommandService CreateSut(Task? returns = default)
        => new(MockRemoteDefinition.Object, MockLogger, returns ?? Task.CompletedTask);

    [TestInitialize]
    public void SetupMocks()
    {
        MockRemoteDefinition
            .SetupGet(x => x.RemoteRoot)
            .Returns(RemoteDefinition)
            .Verifiable(Times.Once);

        MockInitializeActivity
            .SetupSet(x => x.Description = It.IsAny<string>())
            .Verifiable(Times.Never);
        MockInitializeActivity
            .Setup(x => x.SetFatalError(It.IsAny<Exception>()))
            .Callback(delegate (Exception ex) { Assert.Fail("SetFatalError was called on the activity: {0}", ex); });
        MockInitializeActivity
            .Setup(x => x.Dispose())
            .Verifiable(Times.Never);

        MockCleanupActivity
            .SetupSet(x => x.Description = It.IsAny<string>())
            .Verifiable(Times.Never);
        MockCleanupActivity
            .Setup(x => x.SetFatalError(It.IsAny<Exception>()))
            .Callback(delegate (Exception ex) { Assert.Fail("SetFatalError was called on the activity: {0}", ex); });
        MockCleanupActivity
            .Setup(x => x.Dispose())
            .Verifiable(Times.Never);
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockRemoteDefinition.Verify();
    }

    private static string ExpectMessage_Executing(string commandName)
        => $"Information[601]: {string.Format(LoggingMessages.CommandService_Executing, commandName)}";
    private static string ExpectMessage_Executed(string commandName)
        => $"Information[602]: {string.Format(LoggingMessages.CommandService_Executed, commandName)}";
    private static string ExpectMessage_Error(string commandName, Exception error)
        => $"Error[603]: {string.Format(LoggingMessages.CommandService_Error, commandName, error)}";
    private static string ExpectMessage_Cancelled(string commandName)
        => $"Warning[604]: {string.Format(LoggingMessages.CommandService_Cancelled, commandName)}";
    private static string ExpectMessage_NotStarted(string commandName)
        => $"Error[605]: {string.Format(LoggingMessages.CommandService_NotStarted, commandName)}";
    private static string ExpectMessage_WasShutDown(string commandName)
        => $"Error[606]: {string.Format(LoggingMessages.CommandService_WasShutDown, commandName)}";

    [TestMethod]
    public void CommandServiceBase_Name_ReturnsName()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        // Act
        string result = sut.Name;

        // Assert
        result.Should().BeEquivalentTo(nameof(MockCommandService));
    }

    [TestMethod]
    public void CommandServiceBase_Constructor_SetsNotStartedActionsOnCommandsOfCorrectType()
    {
        // Arrange
        List<string> expectedMessages = new();

        // Act
        _ = CreateSut();

        // Assert
        foreach (Command command in RemoteDefinition.Elements)
        {
            if (command is MockCommand)
            {
                command.ExecuteAsync.Should().NotBeNull(because: "a handler should have been added to {0} throw a not-initialized exception", command);
                command.IsEnabled.Should().BeFalse(because: "the service has not initialized {0} yet", command);
                command.IsActive.Should().BeFalse(because: "the service has not initialized {0} yet", command);

                Task executeTask = command.ExecuteAsync!(default);
                executeTask.Should().BeFaultedWith(Errors.CommandService_NotStarted(command),
                    because: "the service has not initialized {0} yet", command);

                expectedMessages.Add(ExpectMessage_NotStarted(command.ToString()));
                MockLogger.VerifyMessages(expectedMessages.ToArray());
            }
            else
            {
                command.ExecuteAsync.Should().BeNull(because: $"{{0}} is not handled by {nameof(MockCommandService)}", command);
                command.IsEnabled.Should().BeTrue(because: $"the properties of {{0}} are not changed by {nameof(MockCommandService)}", command);
                command.IsActive.Should().BeTrue(because: $"the properties of {{0}} are not changed by {nameof(MockCommandService)}", command);
            }
        }
    }

    [TestMethod]
    public void CommandServiceBase_InitializeAsync_SetsExecuteAsyncAndIsEnabled()
    {
        // Arrange
        List<string> expectedMessages = new();
        IScopedLifecycle sut = CreateSut();

        // Act
        Task initializeTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        initializeTask.Should().BeComplete(because: "the service was initialized");

        foreach (Command command in RemoteDefinition.Elements)
        {
            if (command is MockCommand)
            {
                command.ExecuteAsync.Should().NotBeNull(because: "a handler should have been added to execute {0}", command);
                command.IsEnabled.Should().BeTrue(because: "the service has initialized {0}", command);
                command.IsActive.Should().BeFalse(because: "the service is not executing {0}", command);
            }
            else
            {
                command.ExecuteAsync.Should().BeNull(because: $"{{0}} is not handled by {nameof(MockCommandService)}", command);
                command.IsEnabled.Should().BeTrue(because: $"the properties of {{0}} are not changed by {nameof(MockCommandService)}", command);
                command.IsActive.Should().BeTrue(because: $"the properties of {{0}} are not changed by {nameof(MockCommandService)}", command);
            }
        }

        MockLogger.VerifyMessages();
    }

    [TestMethod]
    public void CommandServiceBase_ExecuteAsync_ExecutesCommandFromDerivedClass()
    {
        // Arrange
        List<string> expectedMessages = new();
        MockCommandService sut = CreateSut();
        _ = sut.InitializeAsync(InitializeActivity, default);

        int commandCount = 0;
        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            command.ExecuteAsync.Should().NotBeNull(because: "{0} was initialized", command);

            // Act
            Task executeTask = command.ExecuteAsync!(default);

            // Assert
            executeTask.Should().BeComplete(because: "{0} executes synchronously", command);
            command.IsActive.Should().BeFalse(because: "{0} executes has already completed", command);

            sut.ExecutedCommands.Count.Should().Be(commandCount + 1, because: "that's how many times a command has been executed so far");
            sut.ExecutedCommands[commandCount].Should().BeSameAs(command, because: "that's the last command that was executed");
            commandCount++;

            expectedMessages.Add(ExpectMessage_Executing(command.ToString()));
            expectedMessages.Add(ExpectMessage_Executed(command.ToString()));
            MockLogger.VerifyMessages(expectedMessages.ToArray());
        }
    }

    [TestMethod]
    public void CommandServiceBase_ExecuteAsync_WaitsForCommandFromDerivedClass()
    {
        // Arrange
        List<string> expectedMessages = new();
        MockCommandService sut = CreateSut(returns: new TaskCompletionSource().Task);
        _ = sut.InitializeAsync(InitializeActivity, default);

        int commandCount = 0;
        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            command.ExecuteAsync.Should().NotBeNull(because: "{0} was initialized", command);

            // Act
            Task executeTask = command.ExecuteAsync!(default);

            // Assert
            executeTask.Should().NotBeComplete(because: "{0} returns an incomplete Task", command);
            command.IsActive.Should().Be(true, because: "{0} is still executing", command);

            sut.ExecutedCommands.Count.Should().Be(commandCount + 1, because: "that's how many times a command has been executed so far");
            sut.ExecutedCommands[commandCount].Should().BeSameAs(command, because: "that's the last command that was executed");
            commandCount++;

            expectedMessages.Add(ExpectMessage_Executing(command.ToString()));
            MockLogger.VerifyMessages(expectedMessages.ToArray());
        }
    }

    [TestMethod]
    public void CommandServiceBase_ExecuteAsync_LogsMessageOnErrorInHandler()
    {
        // Arrange
        Exception expectedException = new IndexOutOfRangeException("You want how many fish?!?");

        List<string> expectedMessages = new();
        MockCommandService sut = CreateSut(returns: Task.FromException(expectedException));
        _ = sut.InitializeAsync(InitializeActivity, default);

        int commandCount = 0;
        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            command.ExecuteAsync.Should().NotBeNull(because: "{0} was initialized", command);

            // Act
            Task executeTask = command.ExecuteAsync!(default);

            // Assert
            executeTask.Should().BeFaultedWith(expectedException, because: "{0} throws an exception", command);
            command.IsActive.Should().Be(false, because: "{0} threw an exception and is no longer executing", command);

            sut.ExecutedCommands.Count.Should().Be(commandCount + 1, because: "that's how many times a command has been executed so far");
            sut.ExecutedCommands[commandCount].Should().BeSameAs(command, because: "that's the last command that was executed");
            commandCount++;

            expectedMessages.Add(ExpectMessage_Executing(command.ToString()));
            expectedMessages.Add(ExpectMessage_Error(command.ToString(), expectedException));
            MockLogger.VerifyMessages(expectedMessages.ToArray());
        }
    }

    [TestMethod]
    public void CommandServiceBase_ExecuteAsync_LogsMessageWhenHandlerCancelled()
    {
        // Arrange
        CancellationTokenSource cts = new();
        cts.Cancel();

        List<string> expectedMessages = new();
        MockCommandService sut = CreateSut(returns: Task.FromCanceled(cts.Token));
        _ = sut.InitializeAsync(InitializeActivity, default);

        int commandCount = 0;
        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            command.ExecuteAsync.Should().NotBeNull(because: "{0} was initialized", command);

            // Act
            Task executeTask = command.ExecuteAsync!(default);

            // Assert
            executeTask.Should().BeCanceled(because: "{0} returns a cancelled Task", command);
            command.IsActive.Should().Be(false, because: "{0} was cancelled and is no longer executing", command);

            sut.ExecutedCommands.Count.Should().Be(commandCount + 1, because: "that's how many times a command has been executed so far");
            sut.ExecutedCommands[commandCount].Should().BeSameAs(command, because: "that's the last command that was executed");
            commandCount++;

            expectedMessages.Add(ExpectMessage_Executing(command.ToString()));
            expectedMessages.Add(ExpectMessage_Cancelled(command.ToString()));
            MockLogger.VerifyMessages(expectedMessages.ToArray());
        }
    }

    [TestMethod]
    public void CommandServiceBase_ExecuteAsync_PassesCancellationTokenToHandler()
    {
        // Arrange
        CancellationTokenSource cts = new();

        List<string> expectedMessages = new();
        MockCommandService sut = CreateSut(returns: new TaskCompletionSource().Task);
        _ = sut.InitializeAsync(InitializeActivity, default);

        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            command.ExecuteAsync.Should().NotBeNull(because: "{0} was initialized", command);

            Task executeTask = command.ExecuteAsync!(cts.Token);
        }

        // Act
        cts.Cancel();

        // Assert
        sut.CancelTokens.ForEach(x => x.IsCancellationRequested.Should().Be(true, because: "all executing commands were cancelled"));

        MockLogger.VerifyMessages(
            ExpectMessage_Executing(RemoteDefinition.Elements.OfType<MockCommand>().ElementAt(0).ToString()),
            ExpectMessage_Executing(RemoteDefinition.Elements.OfType<MockCommand>().ElementAt(1).ToString()),
            ExpectMessage_Executing(RemoteDefinition.Elements.OfType<MockCommand>().ElementAt(2).ToString()));
    }

    [TestMethod]
    public void CommandServiceBase_CleanUpAsync_SetsWasShutDownActionsOnCommandsOfCorrectType()
    {
        // Arrange
        List<string> expectedMessages = new();
        IScopedLifecycle sut = CreateSut();
        _ = sut.InitializeAsync(InitializeActivity, default);

        // Act
        Task cleanUpTask = sut.CleanUpAsync(CleanupActivity, default);

        // Assert
        cleanUpTask.Should().BeComplete(because: "no tasks were executing, so cleanup can happen immediately.");

        foreach (Command command in RemoteDefinition.Elements)
        {
            if (command is MockCommand)
            {
                command.ExecuteAsync.Should().NotBeNull(because: "a handler should have been added to {0} throw an already-cleaned-up exception", command);
                command.IsEnabled.Should().BeFalse(because: "the service has uninitialized {0}", command);
                command.IsActive.Should().BeFalse(because: "the service has uninitialized {0}", command);

                Task resultTask = command.ExecuteAsync!(default);
                resultTask.Should().BeFaultedWith(Errors.CommandService_WasShutDown(command),
                    because: "the service has uninitialized {0}", command);

                expectedMessages.Add(ExpectMessage_WasShutDown(command.ToString()));
                MockLogger.VerifyMessages(expectedMessages.ToArray());
            }
            else
            {
                command.ExecuteAsync.Should().BeNull(because: $"{{0}} is not handled by {nameof(MockCommandService)}", command);
                command.IsEnabled.Should().BeTrue(because: $"the properties of {{0}} are not changed by {nameof(MockCommandService)}", command);
                command.IsActive.Should().BeTrue(because: $"the properties of {{0}} are not changed by {nameof(MockCommandService)}", command);
            }
        }
    }

    [TestMethod]
    public void CommandServiceBase_CleanUpAsync_CancelsCommandsInProgress()
    {
        // Arrange
        CancellationTokenSource cts = new();

        List<string> expectedMessages = new();
        MockCommandService sut = CreateSut(returns: new TaskCompletionSource().Task);
        _ = sut.InitializeAsync(InitializeActivity, default);

        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            command.ExecuteAsync.Should().NotBeNull(because: "{0} was initialized", command);

            Task executeTask = command.ExecuteAsync!(cts.Token);
        }

        // Act
        _ = sut.CleanUpAsync(CleanupActivity, default);

        // Assert
        sut.CancelTokens.ForEach(x => x.WaitForCancelled().Should().BeCompleteWithin(TimeSpan.FromMilliseconds(100),
            because: "all executing commands were cancelled"));

        MockLogger.VerifyMessages(
            ExpectMessage_Executing(RemoteDefinition.Elements.OfType<MockCommand>().ElementAt(0).ToString()),
            ExpectMessage_Executing(RemoteDefinition.Elements.OfType<MockCommand>().ElementAt(1).ToString()),
            ExpectMessage_Executing(RemoteDefinition.Elements.OfType<MockCommand>().ElementAt(2).ToString()));
    }

    private class MockCommandService : CommandServiceBase<MockCommand>
    {
        private readonly Task _returns;

        public MockCommandService(IRemoteDefinitionService remoteDefinition, ILogger logger, Task returns)
            : base(nameof(MockCommandService), remoteDefinition, logger)
        {
            _returns = returns;
        }

        public List<Command> ExecutedCommands { get; } = new();
        public List<CancellationToken> CancelTokens { get; } = new();

        protected override Command.ExecuteDelegate CreateHandler(MockCommand command)
        {
            return cancel =>
            {
                ExecutedCommands.Add(command);
                CancelTokens.Add(cancel);
                return _returns;
            };
        }
    }

    private class MockCommand : Command
    {
        public MockCommand(string name)
            : base(name, null, null, null, null, null, speakPhrase: null)
        { }
    }

    private class OtherCommand : Command
    {
        public OtherCommand(string name)
            : base(name, null, null, null, null, null, speakPhrase: null)
        { }
    }
}
