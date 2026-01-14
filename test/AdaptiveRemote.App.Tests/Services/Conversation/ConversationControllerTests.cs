using System.Runtime.CompilerServices;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace AdaptiveRemote.Services.Conversation;

[TestClass]
public class ConversationControllerTests
{
    private static readonly Task IncompleteTask = new TaskCompletionSource().Task;

    private static readonly IRecognizedSpeech StartListeningSpeech = CreateMockSpeech("Hey You!", "system=STARTLISTENING").Object;
    private static readonly IRecognizedSpeech StopListeningSpeech = CreateMockSpeech("Stop Listening", "system=STOPLISTENING").Object;

    private readonly MockLogger<ConversationController, ConversationStateMachine> MockLogger = new();
    private readonly Mock<ISpeechRecognition> MockRecognition = new();
    private readonly Mock<ISpeechSynthesis> MockSynthesis = new();
    private readonly Mock<IRemoteDefinitionService> MockDefinition = new();
    private readonly Mock<IOptionsSnapshot<ConversationSettings>> MockOptions = new();
    private readonly ConversationSettings ConversationSettings = new();
    private readonly Mock<CommandExecute> Command1Execute = new();
    private readonly Mock<CommandExecute> Command2Execute = new();
    private readonly Mock<ILifecycleActivity> MockInitializeActivity = new() { Name = nameof(MockInitializeActivity) };
    private readonly Mock<ILifecycleActivity> MockCleanupActivity = new() { Name = nameof(MockCleanupActivity) };

    private readonly TiVoCommand Command1 = new("Hey you!");
    private readonly TiVoCommand Command2 = new("Test Two");

    private ILifecycleActivity InitializeActivity => MockInitializeActivity.Object;
    private ILifecycleActivity CleanUpActivity => MockCleanupActivity.Object;

    private readonly ConversationView ViewModel = new("MOCKGROUP");

    private bool _allSpeechWasRead = true;

    public TestContext? TestContext { get; set; }

    private static string Expected_Executing(string command)
        => $"Information[210]: {string.Format(LoggingMessages.ConversationController_Executing, command)}";
    private static string Expected_Executed(string command)
        => $"Information[211]: {string.Format(LoggingMessages.ConversationController_Executed, command)}";
    private static string Expected_Retrying(int times, Exception error)
        => $"Warning[206]: {string.Format(LoggingMessages.ConversationController_Retrying, times, $"{error.GetType().FullName}: {error.Message}")}";
    private static string Expected_RetryLimitReached(int times)
        => $"Warning[205]: {string.Format(LoggingMessages.ConversationController_RetryLimitReached, times)}";
    private static string Expected_Starting
        => $"Information[1201]: {LoggingMessages.ScopedBackgroundProcess_Starting}";
    private static string Expected_Started
        => $"Information[1202]: {LoggingMessages.ScopedBackgroundProcess_Started}";
    private static string Expected_Stopping
        => $"Information[1203]: {LoggingMessages.ScopedBackgroundProcess_Stopping}";
    private static string Expected_Stopped
        => $"Information[1204]: {LoggingMessages.ScopedBackgroundProcess_Stopped}";
    private static string Expected_SwitchedToWorkerThread
        => $"Debug[1208]: {LoggingMessages.ScopedBackgroundProcess_SwitchedToWorkerThread.AsMessageTemplate("")}";
    private static string Expected_SwitchingToWorkerThread
        => $"Debug[1209]: {LoggingMessages.ScopedBackgroundProcess_SwitchingToWorkerThread}";

    private void Expect_GetRemoteDefinition(Times times)
        => MockDefinition
            .SetupGet(x => x.RemoteRoot)
            .Returns(new LayoutGroup("COMMANDS",
            [
                Command1,
                Command2
            ]))
            .Verifiable(times);

    private void Expect_Recognition_Recognize()
        => Expect_Recognition_Recognize(Array.Empty<Task<IRecognizedSpeech>>());
    private void Expect_Recognition_Recognize(params IRecognizedSpeech[] result)
        => Expect_Recognition_Recognize(result.Select(x => Task.FromResult(x)).ToArray());
    private void Expect_Recognition_Recognize(params Task<IRecognizedSpeech>[] result)
    {
        MockRecognition
            .Setup(x => x.RecognizeAsync(It.IsAny<CancellationToken>()))
            .Returns(delegate (CancellationToken c) { return EnumerateAsync(result, c); })
            .Verifiable(Times.Once);

        async IAsyncEnumerable<IRecognizedSpeech> EnumerateAsync(
            IEnumerable<Task<IRecognizedSpeech>> results,
            [EnumeratorCancellation] CancellationToken cancellation)
        {
            foreach (Task<IRecognizedSpeech> result in results)
            {
                yield return await result;
            }

            _allSpeechWasRead = true;

            await cancellation.WaitForCancelledAsync();
        }
    }

    private CancellationToken Expect_Recognition_RecognizeAsync_IsCancelled(bool returnWhenCancelled)
    {
        CancellationTokenSource cts = new();
        MockRecognition
            .Setup(x => x.RecognizeAsync(It.IsAny<CancellationToken>()))
            .Returns(WaitForCancelledAsync)
            .Verifiable(Times.Once);
        return cts.Token;

        async IAsyncEnumerable<IRecognizedSpeech> WaitForCancelledAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await cancellationToken.WaitForCancelledAsync();
            cts.Cancel();

            if (!returnWhenCancelled)
            {
                await new TaskCompletionSource().Task;
            }

            yield break;
        }
    }
    private void Expect_Recognition_SetFilter(PhraseKinds expected, Times? times = default)
        => MockRecognition
            .Setup(x => x.SetFilter(expected))
            .Verifiable(times ?? Times.Once());
    private void Expect_Recognition_SetFilter_IsNotCalled()
        => MockRecognition
            .Setup(x => x.SetFilter(It.IsAny<PhraseKinds>()))
            .Verifiable(Times.Never);

    private void Expect_Synthesis_Say(string phrase, Task? completeTask = default, Times? times = default)
        => MockSynthesis
            .Setup(x => x.SayAsync(phrase, It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(completeTask)
            .Verifiable(times ?? Times.Once());

    private void Expect_Recognition_AllExpectedSpeechIsRead()
        => _allSpeechWasRead = false;

    private void Expect_Command1_Execute(Task? returnTask = default, Times? times = default)
        => Command1Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(returnTask)
            .Verifiable(times ?? Times.Once());
    private void Expect_Command2_Execute(Task? returnTask = default, Times? times = default)
        => Command2Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(returnTask)
            .Verifiable(times ?? Times.Once());
    private void Expect_Command2_ExecuteAsync_IsNotCalled()
        => Command2Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);

    private ConversationController CreateSut() => new ConversationController(
        MockOptions.Object,
        MockRecognition.Object,
        MockSynthesis.Object,
        MockLogger,
        new ConversationStateMachine(MockDefinition.Object, MockOptions.Object, new MockLogger<ConversationStateMachine>()),
        ViewModel);

    private static Mock<IRecognizedSpeech> CreateMockSpeech(string text, params string[] semanticValues)
    {
        string? nullValue;

        Mock<IRecognizedSpeech> mockSpeech = new();
        mockSpeech
            .Setup(x => x.ContainsSemanticValue(It.IsAny<string>()))
            .Returns(false);
        mockSpeech
            .Setup(x => x.TryGetSemanticValue(It.IsAny<string>(), out nullValue))
            .Returns(false);

        mockSpeech
            .SetupGet(x => x.Text)
            .Returns(text);
        mockSpeech
            .SetupGet(x => x.Confidence)
            .Returns(99);

        foreach (string semanticValue in semanticValues)
        {
            string[] parts = semanticValue.Split('=');
            string key = parts[0];
            string? value = parts[1];

            mockSpeech
                .Setup(x => x.ContainsSemanticValue(key))
                .Returns(true);
            mockSpeech
                .Setup(x => x.TryGetSemanticValue(key, out value))
                .Returns(true);
        }

        mockSpeech
            .Setup(x => x.ToString())
            .Returns($"{text} ({string.Join(", ", semanticValues)})");

        return mockSpeech;
    }

    [TestInitialize]
    public void InitializeMocks()
    {
        Expect_GetRemoteDefinition(Times.Once());

        Command1Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        Command1.ExecuteAsync = Command1Execute.Object.ExecuteAsync;
        Command1.IsEnabled = true;

        Command2Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        Command2.ExecuteAsync = Command2Execute.Object.ExecuteAsync;
        Command2.IsEnabled = true;

        MockRecognition
            .Setup(x => x.RecognizeAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);

        MockSynthesis
            .Setup(x => x.SayAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);

        MockOptions
            .SetupGet(x => x.Value)
            .Returns(ConversationSettings)
            .Verifiable(Times.Exactly(2));

        MockLogger.OutputWriter = TestContext;

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
    public void VerifyAllMocks()
    {
        MockRecognition.Verify();
        MockSynthesis.Verify();
        Command1Execute.Verify();
        MockDefinition.Verify();
        _allSpeechWasRead.Should().BeTrue(because: "the test should have finished reading all the speech from ISpeechRecognition.RecognizeAsync()");
    }

    [TestMethod]
    public void ConversationController_OnConstruction_InitializesViewModel()
    {
        // Arrange
        Expect_GetRemoteDefinition(Times.Never());
        Expect_Recognition_SetFilter_IsNotCalled();

        ViewModel.IsListening = true;
        ViewModel.StatusMessage = "Status message was not changed";

        // Act
        ConversationController sut = CreateSut();

        // Assert
        ViewModel.IsListening.Should().BeFalse();
        ViewModel.StatusMessage.Should().Be(Phrases.Conversation_WaitingForActivation);
        ViewModel.SpeakingMessage.Should().BeNull();
    }

    [TestMethod]
    public void ConversationController_Start_StartsListeningForAttention()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Expect_Recognition_Recognize();
        Expect_Recognition_AllExpectedSpeechIsRead();
        Expect_Recognition_SetFilter(PhraseKinds.WakeWord);

        // Act
        sut.InitializeAsync(InitializeActivity, default)
            .Wait(1000)
            .Should().BeTrue(because: "InitializeAsync should complete synchronously");

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started);

        ViewModel.IsListening.Should().BeFalse();
        ViewModel.StatusMessage.Should().Be(Phrases.Conversation_ListeningForAttention);
        ViewModel.SpeakingMessage.Should().BeNull();
    }

    [TestMethod]
    public void ConversationController_OnAttention_WaitsToSayImListeningingBeforeListeningForCommands()
    {
        // Arrange
        ConversationController sut = CreateSut();

        TaskCompletionSource<IRecognizedSpeech> tcs = new();

        Expect_Recognition_Recognize(tcs.Task);

        Expect_Synthesis_Say(Phrases.Conversation_ImListening, completeTask: IncompleteTask);

        sut.InitializeAsync(InitializeActivity, default)
            .Wait(1000)
            .Should().BeTrue(because: "InitializeAsync should complete synchronously");

        MockLogger.VerifyMessages( // Wait for expected start-up
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started);

        // Act
        tcs.SetResult(StartListeningSpeech);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started);

        ViewModel.IsListening.Should().BeFalse();
        ViewModel.StatusMessage.Should().Be(Phrases.Conversation_ImListening);
        ViewModel.SpeakingMessage.Should().Be(Phrases.Conversation_ImListening);
    }

    [TestMethod]
    public void ConversationController_OnAttention_CleanUpWaitsForSayingImListening()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Expect_Recognition_Recognize(
            StartListeningSpeech);
        Expect_Recognition_AllExpectedSpeechIsRead();

        TaskCompletionSource tcs = new();
        Expect_Synthesis_Say(Phrases.Conversation_ImListening, tcs.Task);

        sut.InitializeAsync(InitializeActivity, default)
            .Wait(1000)
            .Should().BeTrue(because: "InitializeAsync should complete synchronously");

        MockLogger.VerifyMessages( // Wait for expected start-up
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started);

        // Act
        Task resultTask = sut.CleanUpAsync(CleanUpActivity, default);

        // Assert
        resultTask.Should().NotBeComplete(because: "Synthesis.SayAsync is still running");

        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started,
            Expected_Stopping);

        // Act
        tcs.SetResult();

        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started,
            Expected_Stopping,
            Expected_Stopped);

        ViewModel.IsListening.Should().BeFalse(because: "ConversationController does not listen while cleaning up");
        ViewModel.StatusMessage.Should().BeEmpty(because: "ConversationController has no status while cleaning up");
        ViewModel.SpeakingMessage.Should().BeNull(because: "ConversationController should not speak while cleaning up");
    }

    [TestMethod]
    public void ConversationController_OnAttention_StartsListeningForCommands()
    {
        // Arrange
        ConversationController sut = CreateSut();

        TaskCompletionSource<IRecognizedSpeech> tcs = new();
        Expect_Recognition_Recognize(tcs.Task);
        Expect_Recognition_AllExpectedSpeechIsRead();
        Expect_Recognition_SetFilter(PhraseKinds.Commands, Times.Exactly(2));
        Expect_Synthesis_Say(Phrases.Conversation_ImListening);

        sut.InitializeAsync(InitializeActivity, default)
            .Wait(1000)
            .Should().BeTrue(because: "InitializeAsync should complete synchronously");

        MockLogger.VerifyMessages( // Wait for expected start-up
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started);

        // Act
        tcs.SetResult(StartListeningSpeech);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started);

        ViewModel.IsListening.Should().BeTrue(because: "ConversationController should be listening for commands after attention word is recognized");
        ViewModel.StatusMessage.Should().Be(Phrases.Conversation_ImListening);
        ViewModel.SpeakingMessage.Should().BeNull(because: "ConversationController should not be speaking while listening");
    }

    [TestMethod]
    public void ConversationController_OnFirstCommand_AnnouncesAndExecutesCommand()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Mock<IRecognizedSpeech> result = CreateMockSpeech(Command1.Name, "command=" + Command1.Name);

        Expect_Recognition_Recognize(
           StartListeningSpeech,
           result.Object);
        Expect_Recognition_SetFilter(PhraseKinds.Commands, Times.Exactly(2));

        Expect_Synthesis_Say(Phrases.Conversation_ImListening);
        Expect_Synthesis_Say(Phrases.Conversation_Sent(Command1.Name), IncompleteTask);

        Expect_Command1_Execute(IncompleteTask);

        // Act
        Task initializeTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Executing(Command1.Name),
            Expected_Started);

        initializeTask.Should().BeComplete(because: "InitializeAsync should complete after the service is initialized");

        ViewModel.IsListening.Should().BeFalse(because: "ConversationController should not be listening while speaking");
        ViewModel.StatusMessage.Should().Be(Phrases.Conversation_ImSending);
        ViewModel.SpeakingMessage.Should().Be(Phrases.Conversation_Sent(Command1.Name));
    }

    [TestMethod]
    public void ConversationController_OnCompletedFirstCommand_LogsExecutedCommand()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Mock<IRecognizedSpeech> result1 = CreateMockSpeech(Command1.Name, "command=" + Command1.Name);

        TaskCompletionSource<IRecognizedSpeech> tcs = new();
        Expect_Recognition_Recognize(
            tcs.Task,
            Task.FromResult(result1.Object));

        Expect_Synthesis_Say(Phrases.Conversation_ImListening);
        Expect_Synthesis_Say(Phrases.Conversation_Sent(Command1.Name));

        Expect_Command1_Execute();

        sut.InitializeAsync(InitializeActivity, default)
            .Wait(1000)
            .Should().BeTrue(because: "InitializeAsync should complete synchronously");

        MockLogger.VerifyMessages( // Wait for expected start-up
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started);

        // Act
        tcs.SetResult(StartListeningSpeech);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started,
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name));

        ViewModel.IsListening.Should().BeTrue();
        ViewModel.StatusMessage.Should().Be(Phrases.Conversation_ImListening);
        ViewModel.SpeakingMessage.Should().BeNull();
    }

    [TestMethod]
    public void ConversationController_OnCompletedFirstCommand_DoesNotExecuteNextCommandUntilSayAsyncCompletes()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Mock<IRecognizedSpeech> result1 = CreateMockSpeech(Command1.Name, "command=" + Command1.Name);
        Mock<IRecognizedSpeech> result2 = CreateMockSpeech(Command2.Name, "command=" + Command2.Name);

        Expect_Recognition_Recognize(
            StartListeningSpeech,
            result1.Object,
            result2.Object);

        Expect_Synthesis_Say(Phrases.Conversation_ImListening);
        Expect_Synthesis_Say(Phrases.Conversation_Sent(Command1.Name), IncompleteTask);

        Expect_Command1_Execute();
        Expect_Command2_ExecuteAsync_IsNotCalled();

        // Act
        Task resultTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name),
            Expected_Started);

        resultTask.Should().BeComplete(because: "InitializeAsync should complete after the service is initialized");

        ViewModel.IsListening.Should().BeFalse(because: "ConversationController should not be listening while speaking");
        ViewModel.StatusMessage.Should().Be(Phrases.Conversation_ImSending);
        ViewModel.SpeakingMessage.Should().Be(Phrases.Conversation_Sent(Command1.Name));
    }

    [TestMethod]
    public void ConversationController_OnCompletedFirstCommand_ExecutesSecondCommand()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Mock<IRecognizedSpeech> result1 = CreateMockSpeech(Command1.Name, "command=" + Command1.Name);
        Mock<IRecognizedSpeech> result2 = CreateMockSpeech(Command2.Name, "command=" + Command2.Name);

        Expect_Recognition_Recognize(
            StartListeningSpeech,
            result1.Object,
            result2.Object);

        Expect_Synthesis_Say(Phrases.Conversation_ImListening);
        Expect_Synthesis_Say(Phrases.Conversation_Sent(Command1.Name));
        Expect_Synthesis_Say(Phrases.Conversation_Sent(Command2.Name));

        Expect_Command1_Execute();
        Expect_Command2_Execute();

        // Act
        Task resultTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name),
            Expected_Executing(Command2.Name),
            Expected_Executed(Command2.Name),
            Expected_Started);

        resultTask.Should().BeComplete(because: "InitializeAsync should complete after the service is initialized");

        ViewModel.IsListening.Should().Be(true);
        ViewModel.StatusMessage.Should().Be(Phrases.Conversation_ImListening);
        ViewModel.SpeakingMessage.Should().Be(null);
    }

    [TestMethod]
    public void ConversationController_OnCompletedFirstCommand_CleanUpWaitsForSayingSent()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Mock<IRecognizedSpeech> result1 = CreateMockSpeech(Command1.Name, "command=" + Command1.Name);

        Expect_Recognition_Recognize(
            StartListeningSpeech,
            result1.Object);

        Expect_Synthesis_Say(Phrases.Conversation_ImListening);

        TaskCompletionSource tcs = new();
        Expect_Synthesis_Say(Phrases.Conversation_Sent(Command1.Name), tcs.Task);

        Expect_Command1_Execute();

        sut.InitializeAsync(InitializeActivity, default)
            .Wait(1000)
            .Should().BeTrue(because: "InitializeAsync should complete synchronously");

        MockLogger.VerifyMessages( // Wait for successful startup
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name),
            Expected_Started);

        // Act
        Task resultTask = sut.CleanUpAsync(CleanUpActivity, default);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name),
            Expected_Started,
            Expected_Stopping);

        resultTask.Should().NotBeComplete(because: "Synthesis.SayAsync is still running");

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name),
            Expected_Started,
            Expected_Stopping,
            Expected_Stopped);

        resultTask.Should().BeComplete(because: "CleanupAsync should complete after the service is cleaned up");

        ViewModel.IsListening.Should().BeFalse();
        ViewModel.StatusMessage.Should().BeEmpty();
        ViewModel.SpeakingMessage.Should().BeNull(because: "ConversationController should not be speaking while cleaning up");
    }

    [TestMethod]
    public void ConversationController_OnCommandWithRepeat_LogsExecutedCommandMultipleTimes()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Mock<IRecognizedSpeech> result1 = CreateMockSpeech(Command1.Name, "command=" + Command1.Name, "repeat=3");

        Expect_Recognition_Recognize(
            StartListeningSpeech,
            result1.Object);

        Expect_Synthesis_Say(Phrases.Conversation_ImListening);
        Expect_Synthesis_Say(Phrases.Conversation_Sent(Command1.Name, 3));

        Expect_Command1_Execute(times: Times.Exactly(3));

        // Act
        sut.InitializeAsync(InitializeActivity, default)
            .Wait(1000)
            .Should().BeTrue(because: "InitializeAsync should complete synchronously");

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name),
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name),
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name),
            Expected_Started);

        ViewModel.IsListening.Should().Be(true);
        ViewModel.StatusMessage.Should().Be(Phrases.Conversation_ImListening);
        ViewModel.SpeakingMessage.Should().BeNull();
    }

    [TestMethod]
    public void ConversationController_OnAttentionAndStopListening_WaitsToSayStoppedListeningBeforeListeningForAttention()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Expect_Recognition_Recognize(
            StartListeningSpeech,
            StopListeningSpeech);

        Expect_Synthesis_Say(Phrases.Conversation_ImListening);
        Expect_Synthesis_Say(Phrases.Conversation_StoppedListening, IncompleteTask);

        // Act
        sut.InitializeAsync(InitializeActivity, default)
            .Wait(1000)
            .Should().BeTrue(because: "InitializeAsync should complete synchronously");

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started);

        ViewModel.IsListening.Should().BeFalse();
        ViewModel.StatusMessage.Should().Be(Phrases.Conversation_ListeningForAttention);
        ViewModel.SpeakingMessage.Should().Be(Phrases.Conversation_StoppedListening);
    }

    [TestMethod]
    public void ConversationController_OnAttentionAndStopListening_CleanUpWaitsForSay()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Expect_Recognition_Recognize(
            StartListeningSpeech,
            StopListeningSpeech);

        TaskCompletionSource tcs = new();

        Expect_Synthesis_Say(Phrases.Conversation_ImListening);
        Expect_Synthesis_Say(Phrases.Conversation_StoppedListening, tcs.Task);

        sut.InitializeAsync(InitializeActivity, default)
            .Wait(1000)
            .Should().BeTrue(because: "InitializeAsync should complete synchronously");

        MockLogger.VerifyMessages( // Wait for successful startup
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started);

        // Act
        Task resultTask = sut.CleanUpAsync(CleanUpActivity, default);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started,
            Expected_Stopping);

        resultTask.Should().NotBeComplete(because: "Synthesis.SayAsync is still running");

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started,
            Expected_Stopping,
            Expected_Stopped);

        resultTask.Should().BeComplete(because: "CleanupAsync should complete after the service is cleaned up");

        ViewModel.IsListening.Should().BeFalse();
        ViewModel.StatusMessage.Should().BeEmpty();
        ViewModel.SpeakingMessage.Should().BeNull();
    }

    [TestMethod]
    public void ConversationController_OnAttentionAndStopListening_StopsAndStartsListeningForCommandsAgain()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Expect_Recognition_Recognize(
            StartListeningSpeech,
            StopListeningSpeech,
            StartListeningSpeech);

        Expect_Synthesis_Say(Phrases.Conversation_ImListening, times: Times.Exactly(2));
        Expect_Synthesis_Say(Phrases.Conversation_StoppedListening);

        // Act
        sut.InitializeAsync(InitializeActivity, default)
            .Wait(1000)
            .Should().BeTrue(because: "InitializeAsync should complete synchronously");

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started);

        ViewModel.IsListening.Should().Be(true);
        ViewModel.StatusMessage.Should().Be(Phrases.Conversation_ImListening);
        ViewModel.SpeakingMessage.Should().BeNull();
    }

    [TestMethod]
    public void ConversationController_OnError_RestartsListeningForAttention()
    {
        // Arrange
        Expect_GetRemoteDefinition(Times.Exactly(2));

        ConversationController sut = CreateSut();

        Expect_Recognition_Recognize(
            StartListeningSpeech,
            CreateMockSpeech("Play", "command=" + Command1.Name).Object);

        Expect_Synthesis_Say(Phrases.Conversation_ImListening);
        Expect_Synthesis_Say(Phrases.Conversation_Sent(Command1.Name));

        AccessViolationException exception = new AccessViolationException("Whoopsie!");
        Command1Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Callback(Expect_Recognition_Recognize)
            .Throws(exception);

        // Act
        sut.InitializeAsync(InitializeActivity, default)
            .Wait(1000)
            .Should().BeTrue(because: "InitializeAsync should complete synchronously");

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Executing(Command1.Name),
            Expected_Retrying(1, exception),
            Expected_Started);

        ViewModel.IsListening.Should().BeFalse();
        ViewModel.StatusMessage.Should().Be(Phrases.Conversation_ListeningForAttention);
        ViewModel.SpeakingMessage.Should().BeNull();
    }

    [TestMethod]
    [Timeout(1000)]
    public void ConversationController_OnRepeatedErrors_StopsRestartingAfterErrorLimit()
    {
        // Arrange
        Expect_GetRemoteDefinition(Times.Exactly(ConversationSettings.ErrorRetryLimit));

        ConversationController sut = CreateSut();

        AccessViolationException exception = new AccessViolationException("Whoopsie!");
        Expect_Recognition_Recognize(
            StartListeningSpeech,
            CreateMockSpeech(Command1.Name, "command=" + Command1.Name).Object);

        Expect_Synthesis_Say(Phrases.Conversation_ImListening, times: Times.Exactly(ConversationSettings.ErrorRetryLimit));
        Expect_Synthesis_Say(Phrases.Conversation_Sent(Command1.Name), times: Times.Exactly(ConversationSettings.ErrorRetryLimit));

        int count = 0;
        Command1Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (++count < ConversationSettings.ErrorRetryLimit)
                {
                    Expect_Recognition_Recognize(
                        StartListeningSpeech,
                        CreateMockSpeech(Command1.Name, "command=" + Command1.Name).Object);
                }
            })
            .Throws(exception)
            .Verifiable(Times.Exactly(ConversationSettings.ErrorRetryLimit));

        // Act
        Task initializeTask = sut.InitializeAsync(InitializeActivity, default);

        MockLogger.WaitForMessageAsync(Expected_SwitchedToWorkerThread, TimeSpan.FromSeconds(10)).Wait();

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Executing(Command1.Name),
            Expected_Retrying(1, exception),
            Expected_Executing(Command1.Name),
            Expected_Retrying(2, exception),
            Expected_Executing(Command1.Name),
            Expected_Retrying(3, exception),
            Expected_Executing(Command1.Name),
            Expected_Retrying(4, exception),
            Expected_Executing(Command1.Name),
            Expected_Retrying(5, exception),
            Expected_Executing(Command1.Name),
            Expected_Retrying(6, exception),
            Expected_Executing(Command1.Name),
            Expected_Retrying(7, exception),
            Expected_Executing(Command1.Name),
            Expected_Retrying(8, exception),
            Expected_Executing(Command1.Name),
            Expected_Retrying(9, exception),
            Expected_Executing(Command1.Name),
            Expected_RetryLimitReached(10));

        initializeTask.Should().BeFaultedWith(exception,
            because: "the exception occurred during too many retries");

        ViewModel.IsListening.Should().BeFalse();
        ViewModel.StatusMessage.Should().Be(Phrases.Conversation_SystemFailed);
        ViewModel.SpeakingMessage.Should().BeNull();
    }

    [TestMethod]
    public void ConversationController_CleanUpWhileWaitingForAttention_CancelsWaitingForAttention()
    {
        // Arrange
        ConversationController sut = CreateSut();

        CancellationToken cancelled = Expect_Recognition_RecognizeAsync_IsCancelled(returnWhenCancelled: false);

        sut.InitializeAsync(InitializeActivity, default)
            .Wait(1000)
            .Should().BeTrue(because: "InitializeAsync should complete synchronously");

        MockLogger.VerifyMessages( // Wait for successful startup
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started);

        // Act
        Task resultTask = sut.CleanUpAsync(CleanUpActivity, default);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started,
            Expected_Stopping);

        resultTask.Should().NotBeCompleteWithin(TimeSpan.FromMilliseconds(100), because: "Recognition.RecognizeAsync is still running");

        cancelled.IsCancellationRequested.Should().BeTrue(because: "RecognizeAsync's CancellationToken should have been triggered");

        ViewModel.IsListening.Should().BeFalse();
        ViewModel.StatusMessage.Should().Be(Phrases.Cleanup_ShuttingDown);
        ViewModel.SpeakingMessage.Should().BeNull();
    }

    [TestMethod]
    public void ConversationController_CleanUpCancelsListenForAttention_LogsStopped()
    {
        // Arrange
        ConversationController sut = CreateSut();

        CancellationToken cancelled = Expect_Recognition_RecognizeAsync_IsCancelled(returnWhenCancelled: true);

        sut.InitializeAsync(InitializeActivity, default)
            .Wait(1000)
            .Should().BeTrue(because: "InitializeAsync should complete synchronously");

        MockLogger.VerifyMessages( // Wait for successful startup
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started);

        // Act
        Task resultTask = sut.CleanUpAsync(CleanUpActivity, default);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Started,
            Expected_Stopping,
            Expected_Stopped);

        resultTask.Should().BeComplete(because: "CleanUpAsync should complete after cleanup is finished");

        cancelled.IsCancellationRequested.Should().BeTrue(because: "RecognizeAsync's CancellationToken should have been triggered");

        ViewModel.IsListening.Should().BeFalse();
        ViewModel.StatusMessage.Should().BeEmpty();
        ViewModel.SpeakingMessage.Should().BeNull();
    }

    [TestMethod]
    public void ConversationController_CleanUpWhileExecutingCommand_CancelsExecutingCommand()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Expect_Recognition_Recognize(
            StartListeningSpeech,
            CreateMockSpeech(Command1.Name, "command=" + Command1.Name).Object);

        Expect_Synthesis_Say(Phrases.Conversation_ImListening);
        Expect_Synthesis_Say(Phrases.Conversation_Sent(Command1.Name));

        CancellationToken token = default;
        Command1Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { token = cancel; })
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        sut.InitializeAsync(InitializeActivity, default)
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "InitializeAsync should complete synchronously");

        MockLogger.VerifyMessages( // Wait for successful startup
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Executing(Command1.Name),
            Expected_Started);

        // Act
        Task resultTask = sut.CleanUpAsync(CleanUpActivity, default);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Executing(Command1.Name),
            Expected_Started,
            Expected_Stopping);

        resultTask.Should().NotBeCompleteWithin(TimeSpan.FromMilliseconds(100), because: "Command1Execute is still running");

        token.IsCancellationRequested.Should().BeTrue(because: "ExecuteAsync's CancellationToken should have been triggered");

        ViewModel.IsListening.Should().Be(true);
        ViewModel.StatusMessage.Should().Be(Phrases.Cleanup_ShuttingDown);
        ViewModel.SpeakingMessage.Should().BeNull();
    }

    [TestMethod]
    public void ConversationController_ExecutingCommandCanceled_LogsStopped()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Expect_Recognition_Recognize(
            StartListeningSpeech,
            CreateMockSpeech(Command1.Name, "command=" + Command1.Name).Object);

        Expect_Synthesis_Say(Phrases.Conversation_ImListening);
        Expect_Synthesis_Say(Phrases.Conversation_Sent(Command1.Name));

        TaskCompletionSource tcs = new();
        Command1Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { cancel.Register(tcs.SetCanceled); })
            .Returns(tcs.Task)
            .Verifiable(Times.Once);

        sut.InitializeAsync(InitializeActivity, default)
            .Wait(1000)
            .Should().BeTrue(because: "InitializeAsync should complete synchronously");

        MockLogger.VerifyMessages( // Wait for successful startup
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Executing(Command1.Name),
            Expected_Started);

        // Act
        Task resultTask = sut.CleanUpAsync(CleanUpActivity, default);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_SwitchingToWorkerThread,
            Expected_SwitchedToWorkerThread,
            Expected_Executing(Command1.Name),
            Expected_Started,
            Expected_Stopping,
            Expected_Stopped);

        resultTask.Should().BeComplete(because: "CleanupAsync should complete after the service is cleaned up");

        ViewModel.IsListening.Should().BeFalse();
        ViewModel.StatusMessage.Should().BeEmpty();
        ViewModel.SpeakingMessage.Should().BeNull();
    }

    public abstract class CommandExecute
    {
        public abstract Task ExecuteAsync(CancellationToken cancellationToken);
    }
}
