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
            .Callback(delegate (Exception ex) { Assert.Fail($"SetFatalError was called on the activity: {ex}"); });
        MockInitializeActivity
            .Setup(x => x.Dispose())
            .Verifiable(Times.Never);

        MockCleanupActivity
            .SetupSet(x => x.Description = It.IsAny<string>())
            .Verifiable(Times.Never);
        MockCleanupActivity
            .Setup(x => x.SetFatalError(It.IsAny<Exception>()))
            .Callback(delegate (Exception ex) { Assert.Fail($"SetFatalError was called on the activity: {ex}"); });
        MockCleanupActivity
            .Setup(x => x.Dispose())
            .Verifiable(Times.Never);
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockRemoteDefinition.Verify();
    }

    // Previous helper methods that composed expected log message strings were removed.
    // Tests now verify log calls via the MockLogger messageLogger callback API.

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

                MockLogger.VerifyMessages(messageLogger =>
                {
                    messageLogger.CommandService_NotStarted(command);
                });
                MockLogger.ClearMessages();
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

        MockLogger.VerifyMessages(messageLogger => { });
    }

    [TestMethod]
    public void CommandServiceBase_ExecuteAsync_ExecutesCommandFromDerivedClass()
    {
        // Arrange
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

            MockLogger.VerifyMessages(messageLogger =>
            {
                messageLogger.CommandService_Executing(command);
                messageLogger.CommandService_Executed(command);
            });
            MockLogger.ClearMessages();
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

            // Verify cumulative executing messages up to the current command
            MockLogger.VerifyMessages(messageLogger =>
            {
                messageLogger.CommandService_Executing(command);
            });
            MockLogger.ClearMessages();
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

            MockLogger.VerifyMessages(messageLogger =>
            {
                messageLogger.CommandService_Executing(command);
                messageLogger.CommandService_Error(command, expectedException);
            });
            MockLogger.ClearMessages();
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

            MockLogger.VerifyMessages(messageLogger =>
            {
                messageLogger.CommandService_Executing(command);
                messageLogger.CommandService_Cancelled(command);
            });
            MockLogger.ClearMessages();
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

        MockLogger.VerifyMessages(messageLogger =>
        {
            List<MockCommand> list = RemoteDefinition.Elements.OfType<MockCommand>().ToList();
            messageLogger.CommandService_Executing(list[0]);
            messageLogger.CommandService_Executing(list[1]);
            messageLogger.CommandService_Executing(list[2]);
        });
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

                MockLogger.VerifyMessages(messageLogger =>
                {
                    messageLogger.CommandService_WasShutDown(command);
                });
                MockLogger.ClearMessages();
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
        sut.CancelTokens.ForEach(x => x.WaitForCancelledAsync().Should().BeCompleteWithin(TimeSpan.FromMilliseconds(100),
            because: "all executing commands were cancelled"));

        MockLogger.VerifyMessages(messageLogger =>
        {
            messageLogger.CommandService_Executing(RemoteDefinition.Elements.OfType<MockCommand>().ElementAt(0));
            messageLogger.CommandService_Executing(RemoteDefinition.Elements.OfType<MockCommand>().ElementAt(1));
            messageLogger.CommandService_Executing(RemoteDefinition.Elements.OfType<MockCommand>().ElementAt(2));
        });
    }

    private MockProgrammableCommandService CreateProgrammableSut(Task? executeReturns = default, Task? programReturns = default)
        => new(MockRemoteDefinition.Object, MockLogger, executeReturns ?? Task.CompletedTask, programReturns ?? Task.CompletedTask);

    [TestMethod]
    public void CommandServiceBase_Constructor_LeavesProgramAsyncNullWhenNoProgramHandler()
    {
        // Arrange & Act
        _ = CreateSut();

        // Assert
        foreach (Command command in RemoteDefinition.Elements)
        {
            if (command is MockCommand)
            {
                command.ProgramAsync.Should().BeNull(because: "{0} is not programmable (service does not override CreateProgramHandler)", command);
            }
        }
    }

    [TestMethod]
    public void CommandServiceBase_Constructor_LeavesProgramAsyncNullEvenForProgrammableService()
    {
        // Arrange & Act
        _ = CreateProgrammableSut();

        // Assert
        foreach (Command command in RemoteDefinition.Elements)
        {
            if (command is MockCommand)
            {
                command.ProgramAsync.Should().BeNull(because: "{0} ProgramAsync is not set until InitializeAsync is called", command);
            }
        }
    }

    [TestMethod]
    public void CommandServiceBase_InitializeAsync_SetsProgramAsyncForProgrammableService()
    {
        // Arrange
        IScopedLifecycle sut = CreateProgrammableSut();

        // Act
        Task initializeTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        initializeTask.Should().BeComplete(because: "the service was initialized");

        foreach (Command command in RemoteDefinition.Elements)
        {
            if (command is MockCommand)
            {
                command.ProgramAsync.Should().NotBeNull(because: "a program handler should have been added to {0}", command);
            }
        }

        MockLogger.VerifyMessages(messageLogger => { });
    }

    [TestMethod]
    public void CommandServiceBase_InitializeAsync_LeavesProgramAsyncNullForNonProgrammableService()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        // Act
        Task initializeTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        initializeTask.Should().BeComplete(because: "the service was initialized");

        foreach (Command command in RemoteDefinition.Elements)
        {
            if (command is MockCommand)
            {
                command.ProgramAsync.Should().BeNull(because: "{0} is not programmable and ProgramAsync should remain null", command);
            }
        }

        MockLogger.VerifyMessages(messageLogger => { });
    }

    [TestMethod]
    public void CommandServiceBase_ProgramAsync_ProgramsCommandFromDerivedClass()
    {
        // Arrange
        MockProgrammableCommandService sut = CreateProgrammableSut();
        _ = sut.InitializeAsync(InitializeActivity, default);

        int commandCount = 0;
        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            command.ProgramAsync.Should().NotBeNull(because: "{0} was initialized", command);

            // Act
            Task programTask = command.ProgramAsync!(default);

            // Assert
            programTask.Should().BeComplete(because: "{0} programs synchronously", command);
            command.IsActive.Should().BeFalse(because: "{0} programming has already completed", command);

            sut.ProgrammedCommands.Count.Should().Be(commandCount + 1, because: "that's how many times a command has been programmed so far");
            sut.ProgrammedCommands[commandCount].Should().BeSameAs(command, because: "that's the last command that was programmed");
            commandCount++;

            MockLogger.VerifyMessages(messageLogger =>
            {
                messageLogger.CommandService_Programming(command);
                messageLogger.CommandService_Programmed(command);
            });
            MockLogger.ClearMessages();
        }
    }

    [TestMethod]
    public void CommandServiceBase_ProgramAsync_WaitsForProgramHandlerFromDerivedClass()
    {
        // Arrange
        MockProgrammableCommandService sut = CreateProgrammableSut(programReturns: new TaskCompletionSource().Task);
        _ = sut.InitializeAsync(InitializeActivity, default);

        int commandCount = 0;
        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            command.ProgramAsync.Should().NotBeNull(because: "{0} was initialized", command);

            // Act
            Task programTask = command.ProgramAsync!(default);

            // Assert
            programTask.Should().NotBeComplete(because: "{0} returns an incomplete Task", command);
            command.IsActive.Should().Be(true, because: "{0} is still programming", command);

            sut.ProgrammedCommands.Count.Should().Be(commandCount + 1, because: "that's how many times a command has been programmed so far");
            sut.ProgrammedCommands[commandCount].Should().BeSameAs(command, because: "that's the last command that was programmed");
            commandCount++;

            MockLogger.VerifyMessages(messageLogger =>
            {
                messageLogger.CommandService_Programming(command);
            });
            MockLogger.ClearMessages();
        }
    }

    [TestMethod]
    public void CommandServiceBase_ProgramAsync_SetsIsActiveDuringProgramming()
    {
        // Arrange
        TaskCompletionSource tcs = new();
        MockProgrammableCommandService sut = CreateProgrammableSut(programReturns: tcs.Task);
        _ = sut.InitializeAsync(InitializeActivity, default);

        MockCommand command = RemoteDefinition.Elements.OfType<MockCommand>().First();

        // Act
        Task programTask = command.ProgramAsync!(default);

        // Assert - IsActive is true during programming
        command.IsActive.Should().BeTrue(because: "{0} is still programming", command);

        // Complete the task
        tcs.SetResult();

        programTask.Should().BeComplete(because: "{0} programming has completed", command);
        command.IsActive.Should().BeFalse(because: "{0} programming has completed", command);

        MockLogger.VerifyMessages(messageLogger =>
        {
            messageLogger.CommandService_Programming(command);
            messageLogger.CommandService_Programmed(command);
        });
    }

    [TestMethod]
    public void CommandServiceBase_ProgramAsync_LogsMessageOnErrorInHandler()
    {
        // Arrange
        Exception expectedException = new InvalidOperationException("Programming device not found");

        MockProgrammableCommandService sut = CreateProgrammableSut(programReturns: Task.FromException(expectedException));
        _ = sut.InitializeAsync(InitializeActivity, default);

        int commandCount = 0;
        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            command.ProgramAsync.Should().NotBeNull(because: "{0} was initialized", command);

            // Act
            Task programTask = command.ProgramAsync!(default);

            // Assert
            programTask.Should().BeFaultedWith(expectedException, because: "{0} throws an exception", command);
            command.IsActive.Should().Be(false, because: "{0} threw an exception and is no longer programming", command);

            sut.ProgrammedCommands.Count.Should().Be(commandCount + 1, because: "that's how many times a command has been programmed so far");
            sut.ProgrammedCommands[commandCount].Should().BeSameAs(command, because: "that's the last command that was programmed");
            commandCount++;

            MockLogger.VerifyMessages(messageLogger =>
            {
                messageLogger.CommandService_Programming(command);
                messageLogger.CommandService_ProgramError(command, expectedException);
            });
            MockLogger.ClearMessages();
        }
    }

    [TestMethod]
    public void CommandServiceBase_ProgramAsync_LogsMessageWhenHandlerCancelled()
    {
        // Arrange
        CancellationTokenSource cts = new();
        cts.Cancel();

        MockProgrammableCommandService sut = CreateProgrammableSut(programReturns: Task.FromCanceled(cts.Token));
        _ = sut.InitializeAsync(InitializeActivity, default);

        int commandCount = 0;
        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            command.ProgramAsync.Should().NotBeNull(because: "{0} was initialized", command);

            // Act
            Task programTask = command.ProgramAsync!(default);

            // Assert
            programTask.Should().BeCanceled(because: "{0} returns a cancelled Task", command);
            command.IsActive.Should().Be(false, because: "{0} was cancelled and is no longer programming", command);

            sut.ProgrammedCommands.Count.Should().Be(commandCount + 1, because: "that's how many times a command has been programmed so far");
            sut.ProgrammedCommands[commandCount].Should().BeSameAs(command, because: "that's the last command that was programmed");
            commandCount++;

            MockLogger.VerifyMessages(messageLogger =>
            {
                messageLogger.CommandService_Programming(command);
                messageLogger.CommandService_ProgramCancelled(command);
            });
            MockLogger.ClearMessages();
        }
    }

    [TestMethod]
    public void CommandServiceBase_ProgramAsync_PassesCancellationTokenToHandler()
    {
        // Arrange
        CancellationTokenSource cts = new();

        MockProgrammableCommandService sut = CreateProgrammableSut(programReturns: new TaskCompletionSource().Task);
        _ = sut.InitializeAsync(InitializeActivity, default);

        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            command.ProgramAsync.Should().NotBeNull(because: "{0} was initialized", command);

            Task programTask = command.ProgramAsync!(cts.Token);
        }

        // Act
        cts.Cancel();

        // Assert
        sut.CancelTokens.ForEach(x => x.IsCancellationRequested.Should().Be(true, because: "all programming commands were cancelled"));

        MockLogger.VerifyMessages(messageLogger =>
        {
            List<MockCommand> list = RemoteDefinition.Elements.OfType<MockCommand>().ToList();
            messageLogger.CommandService_Programming(list[0]);
            messageLogger.CommandService_Programming(list[1]);
            messageLogger.CommandService_Programming(list[2]);
        });
    }

    [TestMethod]
    public void CommandServiceBase_CleanUpAsync_SetsProgramWasShutDownHandlerForProgrammableCommands()
    {
        // Arrange
        IScopedLifecycle sut = CreateProgrammableSut();
        _ = sut.InitializeAsync(InitializeActivity, default);

        // Act
        Task cleanUpTask = sut.CleanUpAsync(CleanupActivity, default);

        // Assert
        cleanUpTask.Should().BeComplete(because: "no tasks were executing, so cleanup can happen immediately.");

        foreach (Command command in RemoteDefinition.Elements)
        {
            if (command is MockCommand)
            {
                command.ProgramAsync.Should().NotBeNull(because: "a was-shut-down handler should be set on {0}", command);
                command.IsEnabled.Should().BeFalse(because: "the service has uninitialized {0}", command);

                Task resultTask = command.ProgramAsync!(default);
                resultTask.Should().BeFaultedWith(Errors.CommandService_ProgramWasShutDown(command),
                    because: "the service has been shut down for {0}", command);

                MockLogger.VerifyMessages(messageLogger =>
                {
                    messageLogger.CommandService_ProgramWasShutDown(command);
                });
                MockLogger.ClearMessages();
            }
        }
    }

    [TestMethod]
    public void CommandServiceBase_CleanUpAsync_LeavesProgramAsyncNullForNonProgrammableCommands()
    {
        // Arrange
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
                command.ProgramAsync.Should().BeNull(because: "{0} is not programmable and ProgramAsync should stay null after cleanup", command);
            }
        }
    }

    [TestMethod]
    public void CommandServiceBase_CleanUpAsync_CancelsProgrammingInProgress()
    {
        // Arrange
        CancellationTokenSource cts = new();

        MockProgrammableCommandService sut = CreateProgrammableSut(programReturns: new TaskCompletionSource().Task);
        _ = sut.InitializeAsync(InitializeActivity, default);

        foreach (MockCommand command in RemoteDefinition.Elements.OfType<MockCommand>())
        {
            command.ProgramAsync.Should().NotBeNull(because: "{0} was initialized", command);

            Task programTask = command.ProgramAsync!(cts.Token);
        }

        // Act
        _ = sut.CleanUpAsync(CleanupActivity, default);

        // Assert
        sut.CancelTokens.ForEach(x => x.WaitForCancelledAsync().Should().BeCompleteWithin(TimeSpan.FromMilliseconds(100),
            because: "all programming commands were cancelled"));

        MockLogger.VerifyMessages(messageLogger =>
        {
            messageLogger.CommandService_Programming(RemoteDefinition.Elements.OfType<MockCommand>().ElementAt(0));
            messageLogger.CommandService_Programming(RemoteDefinition.Elements.OfType<MockCommand>().ElementAt(1));
            messageLogger.CommandService_Programming(RemoteDefinition.Elements.OfType<MockCommand>().ElementAt(2));
        });
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

    private class MockProgrammableCommandService : CommandServiceBase<MockCommand>
    {
        private readonly Task _executeReturns;
        private readonly Task _programReturns;

        public MockProgrammableCommandService(IRemoteDefinitionService remoteDefinition, ILogger logger, Task executeReturns, Task programReturns)
            : base(nameof(MockProgrammableCommandService), remoteDefinition, logger)
        {
            _executeReturns = executeReturns;
            _programReturns = programReturns;
        }

        public List<Command> ExecutedCommands { get; } = new();
        public List<Command> ProgrammedCommands { get; } = new();
        public List<CancellationToken> CancelTokens { get; } = new();

        protected override Command.ExecuteDelegate CreateHandler(MockCommand command)
        {
            return cancel =>
            {
                ExecutedCommands.Add(command);
                CancelTokens.Add(cancel);
                return _executeReturns;
            };
        }

        protected override Command.ExecuteDelegate? CreateProgramHandler(MockCommand command)
        {
            return cancel =>
            {
                ProgrammedCommands.Add(command);
                CancelTokens.Add(cancel);
                return _programReturns;
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
