using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using AdaptiveRemote.TestUtilities;
using AdaptiveRemote.Utilities;
using Microsoft.Extensions.Logging;
using Moq;

namespace AdaptiveRemote.Services;

[TestClass]
public class CommandServiceBaseTests
{
    private readonly Mock<IRemoteDefinitionService> MockRemoteDefinition = new();
    private readonly MockLogger<MockCommandService> MockLogger = new();

    private LayoutGroup RemoteDefinition = new LayoutGroup("ROOT",
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
        Assert.AreEqual(nameof(MockCommandService), result, nameof(result));
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
                Assert.IsNotNull(command.ExecuteAsync, nameof(command.ExecuteAsync) + " for {0}", command.Name);
                Assert.IsFalse(command.IsEnabled, nameof(command.IsEnabled) + " for {0}", command.Name);
                Assert.IsFalse(command.IsActive, nameof(command.IsActive) + " for {0}", command.Name);

                Task resultTask = command.ExecuteAsync(default);
                TaskAssert.IsFaulted(resultTask, Errors.CommandService_NotStarted(command), nameof(resultTask) + " for {0}", command.Name);

                expectedMessages.Add(ExpectMessage_NotStarted(command.ToString()));
                MockLogger.VerifyMessages(expectedMessages.ToArray());
            }
            else
            {
                Assert.IsNull(command.ExecuteAsync, nameof(command.ExecuteAsync) + " for {0}", command.Name);
                Assert.IsTrue(command.IsEnabled, nameof(command.IsEnabled) + " for {0}", command.Name);
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
        Task initializeTask = sut.InitializeAsync(default);

        // Assert
        TaskAssert.IsComplete(initializeTask, nameof(initializeTask));

        foreach (Command command in RemoteDefinition.Elements)
        {
            if (command is MockCommand)
            {
                Assert.IsNotNull(command.ExecuteAsync, nameof(command.ExecuteAsync) + " for {0}", command.Name);
                Assert.IsTrue(command.IsEnabled, nameof(command.IsEnabled) + " for {0}", command.Name);
                Assert.IsFalse(command.IsActive, nameof(command.IsActive) + " for {0}", command.Name);
            }
            else
            {
                Assert.IsNull(command.ExecuteAsync, nameof(command.ExecuteAsync) + " for {0}", command.Name);
                Assert.IsTrue(command.IsEnabled, nameof(command.IsEnabled) + " for {0}", command.Name);
                Assert.IsTrue(command.IsActive, nameof(command.IsActive) + " for {0}", command.Name);
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
        sut.InitializeAsync(default);

        int commandCount = 0;
        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            Assert.IsNotNull(command.ExecuteAsync, nameof(command.ExecuteAsync) + " was not set for {0}", command.Name);

            // Act
            Task executeTask = command.ExecuteAsync(default);

            // Assert
            TaskAssert.IsComplete(executeTask, nameof(executeTask) + " for {0}", command.Name);
            Assert.IsFalse(command.IsActive, nameof(command.IsActive) + " for {0}", command.Name);

            Assert.AreEqual(commandCount + 1, sut.ExecutedCommands.Count, nameof(MockCommandService.ExecutedCommands));
            Assert.AreSame(command, sut.ExecutedCommands[commandCount], nameof(MockCommandService.ExecutedCommands) + "[{0}]", commandCount);
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
        _ = sut.InitializeAsync(default);

        int commandCount = 0;
        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            Assert.IsNotNull(command.ExecuteAsync, nameof(command.ExecuteAsync) + " was not set for {0}", command.Name);

            // Act
            Task executeTask = command.ExecuteAsync(default);

            // Assert
            TaskAssert.IsNotComplete(executeTask, nameof(executeTask) + " for {0}", command.Name);
            Assert.IsTrue(command.IsActive, nameof(command.IsActive));

            Assert.AreEqual(commandCount + 1, sut.ExecutedCommands.Count, nameof(MockCommandService.ExecutedCommands));
            Assert.AreSame(command, sut.ExecutedCommands[commandCount], nameof(MockCommandService.ExecutedCommands) + "[{0}]", commandCount);
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
        _ = sut.InitializeAsync(default);

        int commandCount = 0;
        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            Assert.IsNotNull(command.ExecuteAsync, nameof(command.ExecuteAsync) + " was not set for {0}", command.Name);

            // Act
            Task executeTask = command.ExecuteAsync(default);

            // Assert
            TaskAssert.IsFaulted(executeTask, expectedException, nameof(executeTask) + " for {0}", command.Name);
            Assert.IsFalse(command.IsActive, nameof(command.IsActive) + " for {0}", command.Name);

            Assert.AreEqual(commandCount + 1, sut.ExecutedCommands.Count, nameof(MockCommandService.ExecutedCommands));
            Assert.AreSame(command, sut.ExecutedCommands[commandCount], nameof(MockCommandService.ExecutedCommands) + "[{0}]", commandCount);
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
        _ = sut.InitializeAsync(default);

        int commandCount = 0;
        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            Assert.IsNotNull(command.ExecuteAsync, nameof(command.ExecuteAsync) + " was not set for {0}", command.Name);

            // Act
            Task executeTask = command.ExecuteAsync(default);

            // Assert
            TaskAssert.IsCanceled(executeTask, nameof(executeTask) + " for {0}", command.Name);
            Assert.IsFalse(command.IsActive, nameof(command.IsActive) + " for {0}", command.Name);

            Assert.AreEqual(commandCount + 1, sut.ExecutedCommands.Count, nameof(MockCommandService.ExecutedCommands));
            Assert.AreSame(command, sut.ExecutedCommands[commandCount], nameof(MockCommandService.ExecutedCommands) + "[{0}]", commandCount);
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
        _ = sut.InitializeAsync(default);

        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            Assert.IsNotNull(command.ExecuteAsync, nameof(command.ExecuteAsync) + " was not set for {0}", command.Name);

            Task executeTask = command.ExecuteAsync(cts.Token);
        }

        // Act
        cts.Cancel();

        // Assert
        Assert.IsTrue(sut.CancelTokens.All(x => x.IsCancellationRequested), "All CancelTokens should be cancelled");

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
        _ = sut.InitializeAsync(default);

        // Act
        Task cleanUpTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsComplete(cleanUpTask, nameof(cleanUpTask));

        foreach (Command command in RemoteDefinition.Elements)
        {
            if (command is MockCommand)
            {
                Assert.IsNotNull(command.ExecuteAsync, nameof(command.ExecuteAsync) + " for {0}", command.Name);
                Assert.IsFalse(command.IsEnabled, nameof(command.IsEnabled) + " for {0}", command.Name);
                Assert.IsFalse(command.IsActive, nameof(command.IsActive) + " for {0}", command.Name);

                Task resultTask = command.ExecuteAsync(default);
                TaskAssert.IsFaulted(resultTask, Errors.CommandService_WasShutDown(command), nameof(resultTask) + " for {0}", command.Name);

                expectedMessages.Add(ExpectMessage_WasShutDown(command.ToString()));
                MockLogger.VerifyMessages(expectedMessages.ToArray());
            }
            else
            {
                Assert.IsNull(command.ExecuteAsync, nameof(command.ExecuteAsync) + " for {0}", command.Name);
                Assert.IsTrue(command.IsEnabled, nameof(command.IsEnabled) + " for {0}", command.Name);
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
        _ = sut.InitializeAsync(default);

        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            Assert.IsNotNull(command.ExecuteAsync, nameof(command.ExecuteAsync) + " was not set for {0}", command.Name);

            Task executeTask = command.ExecuteAsync(cts.Token);
        }

        // Act
        sut.CleanUpAsync(default);

        // Assert
        Assert.AreEqual(3, sut.CancelTokens.Count(x => x.IsCancellationRequested), "All CancelTokens should be cancelled");

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
