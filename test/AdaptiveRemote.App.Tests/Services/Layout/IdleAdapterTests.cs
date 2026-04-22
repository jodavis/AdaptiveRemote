using AdaptiveRemote.Models;
using AdaptiveRemote.Services.CloudAssets;
using FluentAssertions;
using Moq;

namespace AdaptiveRemote.Services.Layout;

[TestClass]
public class IdleAdapterTests
{
    private readonly Mock<IIdleDetector> MockIdleDetector = new();
    private readonly Mock<IDisposable> MockToken = new();
    private readonly Mock<ILifecycleActivity> MockActivity = new();

    private ILifecycleActivity Activity => MockActivity.Object;

    [TestInitialize]
    public void SetupMocks()
    {
        MockIdleDetector
            .Setup(d => d.StartNonIdle())
            .Returns(MockToken.Object);
    }

    // ── ConversationIdleAdapter ──────────────────────────────────────────────

    [TestMethod]
    public void ConversationIdleAdapter_InitializeAsync_SubscribesToIsListening()
    {
        // Arrange
        ConversationView conversation = new();
        ConversationIdleAdapter sut = MakeConversationAdapter(conversation);

        // Act
        sut.InitializeAsync(Activity, CancellationToken.None);

        // No token yet — IsListening is false by default
        MockIdleDetector.Verify(d => d.StartNonIdle(), Times.Never);
    }

    [TestMethod]
    public void ConversationIdleAdapter_InitializeAsync_HoldsTokenWhenAlreadyListening()
    {
        // Arrange
        ConversationView conversation = new() { IsListening = true };
        ConversationIdleAdapter sut = MakeConversationAdapter(conversation);

        // Act
        sut.InitializeAsync(Activity, CancellationToken.None);

        // Assert
        MockIdleDetector.Verify(d => d.StartNonIdle(), Times.Once);
    }

    [TestMethod]
    public void ConversationIdleAdapter_IsListeningBecomesTrue_HoldsNonIdleToken()
    {
        // Arrange
        ConversationView conversation = new();
        ConversationIdleAdapter sut = MakeConversationAdapter(conversation);
        sut.InitializeAsync(Activity, CancellationToken.None);

        // Act
        conversation.IsListening = true;

        // Assert
        MockIdleDetector.Verify(d => d.StartNonIdle(), Times.Once);
        MockToken.Verify(t => t.Dispose(), Times.Never);
    }

    [TestMethod]
    public void ConversationIdleAdapter_IsListeningBecomesFalse_DisposesToken()
    {
        // Arrange
        ConversationView conversation = new();
        ConversationIdleAdapter sut = MakeConversationAdapter(conversation);
        sut.InitializeAsync(Activity, CancellationToken.None);
        conversation.IsListening = true;

        // Act
        conversation.IsListening = false;

        // Assert
        MockToken.Verify(t => t.Dispose(), Times.Once);
    }

    [TestMethod]
    public void ConversationIdleAdapter_CleanUpAsync_DisposesToken()
    {
        // Arrange
        ConversationView conversation = new();
        ConversationIdleAdapter sut = MakeConversationAdapter(conversation);
        sut.InitializeAsync(Activity, CancellationToken.None);
        conversation.IsListening = true;

        // Act
        sut.CleanUpAsync(Activity, CancellationToken.None);

        // Assert
        MockToken.Verify(t => t.Dispose(), Times.Once);
    }

    // ── ProgrammingModeIdleAdapter ───────────────────────────────────────────

    [TestMethod]
    public void ProgrammingModeIdleAdapter_InitializeAsync_NoTokenWhenNotProgramming()
    {
        // Arrange
        LifecycleView view = new();
        ProgrammingModeIdleAdapter sut = MakeProgrammingAdapter(view);

        // Act
        sut.InitializeAsync(Activity, CancellationToken.None);

        // Assert
        MockIdleDetector.Verify(d => d.StartNonIdle(), Times.Never);
    }

    [TestMethod]
    public void ProgrammingModeIdleAdapter_InitializeAsync_HoldsTokenWhenAlreadyProgramming()
    {
        // Arrange
        LifecycleView view = new() { IsProgrammingMode = true };
        ProgrammingModeIdleAdapter sut = MakeProgrammingAdapter(view);

        // Act
        sut.InitializeAsync(Activity, CancellationToken.None);

        // Assert
        MockIdleDetector.Verify(d => d.StartNonIdle(), Times.Once);
    }

    [TestMethod]
    public void ProgrammingModeIdleAdapter_IsProgrammingModeBecomesTrue_HoldsNonIdleToken()
    {
        // Arrange
        LifecycleView view = new();
        ProgrammingModeIdleAdapter sut = MakeProgrammingAdapter(view);
        sut.InitializeAsync(Activity, CancellationToken.None);

        // Act
        view.IsProgrammingMode = true;

        // Assert
        MockIdleDetector.Verify(d => d.StartNonIdle(), Times.Once);
        MockToken.Verify(t => t.Dispose(), Times.Never);
    }

    [TestMethod]
    public void ProgrammingModeIdleAdapter_IsProgrammingModeBecomesFalse_DisposesToken()
    {
        // Arrange
        LifecycleView view = new();
        ProgrammingModeIdleAdapter sut = MakeProgrammingAdapter(view);
        sut.InitializeAsync(Activity, CancellationToken.None);
        view.IsProgrammingMode = true;

        // Act
        view.IsProgrammingMode = false;

        // Assert
        MockToken.Verify(t => t.Dispose(), Times.Once);
    }

    [TestMethod]
    public void ProgrammingModeIdleAdapter_CleanUpAsync_DisposesToken()
    {
        // Arrange
        LifecycleView view = new() { IsProgrammingMode = true };
        ProgrammingModeIdleAdapter sut = MakeProgrammingAdapter(view);
        sut.InitializeAsync(Activity, CancellationToken.None);

        // Act
        sut.CleanUpAsync(Activity, CancellationToken.None);

        // Assert
        MockToken.Verify(t => t.Dispose(), Times.Once);
    }

    // ── CommandExecutionIdleAdapter ──────────────────────────────────────────

    [TestMethod]
    public void CommandExecutionIdleAdapter_InitializeAsync_NoTokenWhenNoCommandActive()
    {
        // Arrange
        TiVoCommand command = new TiVoCommand("Play", null, "Play", null, "Play", null, "Play");
        CommandExecutionIdleAdapter sut = MakeCommandAdapter(command);

        // Act
        sut.InitializeAsync(Activity, CancellationToken.None);

        // Assert
        MockIdleDetector.Verify(d => d.StartNonIdle(), Times.Never);
    }

    [TestMethod]
    public void CommandExecutionIdleAdapter_InitializeAsync_HoldsTokenForAlreadyActiveCommand()
    {
        // Arrange
        TiVoCommand command = new TiVoCommand("Play", null, "Play", null, "Play", null, "Play") { IsActive = true };
        CommandExecutionIdleAdapter sut = MakeCommandAdapter(command);

        // Act
        sut.InitializeAsync(Activity, CancellationToken.None);

        // Assert
        MockIdleDetector.Verify(d => d.StartNonIdle(), Times.Once);
    }

    [TestMethod]
    public void CommandExecutionIdleAdapter_IsActiveBecomesTrue_HoldsNonIdleToken()
    {
        // Arrange
        TiVoCommand command = new TiVoCommand("Play", null, "Play", null, "Play", null, "Play");
        CommandExecutionIdleAdapter sut = MakeCommandAdapter(command);
        sut.InitializeAsync(Activity, CancellationToken.None);

        // Act
        command.IsActive = true;

        // Assert
        MockIdleDetector.Verify(d => d.StartNonIdle(), Times.Once);
        MockToken.Verify(t => t.Dispose(), Times.Never);
    }

    [TestMethod]
    public void CommandExecutionIdleAdapter_IsActiveBecomesFalse_DisposesToken()
    {
        // Arrange
        TiVoCommand command = new TiVoCommand("Play", null, "Play", null, "Play", null, "Play");
        CommandExecutionIdleAdapter sut = MakeCommandAdapter(command);
        sut.InitializeAsync(Activity, CancellationToken.None);
        command.IsActive = true;

        // Act
        command.IsActive = false;

        // Assert
        MockToken.Verify(t => t.Dispose(), Times.Once);
    }

    [TestMethod]
    public void CommandExecutionIdleAdapter_MultipleCommands_HoldsOneTokenPerActiveCommand()
    {
        // Arrange
        TiVoCommand play = new TiVoCommand("Play", null, "Play", null, "Play", null, "Play");
        TiVoCommand pause = new TiVoCommand("Pause", null, "Pause", null, "Pause", null, "Pause");

        Mock<IDisposable> playToken = new();
        Mock<IDisposable> pauseToken = new();
        int callCount = 0;
        MockIdleDetector
            .Setup(d => d.StartNonIdle())
            .Returns(() => ++callCount == 1 ? playToken.Object : pauseToken.Object);

        Mock<IRemoteDefinitionService> mockDefinition = new();
        mockDefinition.SetupGet(d => d.RemoteRoot).Returns(new LayoutGroup("ROOT", [play, pause]));
        CommandExecutionIdleAdapter sut = new(mockDefinition.Object, MockIdleDetector.Object);
        sut.InitializeAsync(Activity, CancellationToken.None);

        // Act
        play.IsActive = true;
        pause.IsActive = true;

        // Assert: two separate tokens acquired
        MockIdleDetector.Verify(d => d.StartNonIdle(), Times.Exactly(2));

        // Release one
        play.IsActive = false;
        playToken.Verify(t => t.Dispose(), Times.Once);
        pauseToken.Verify(t => t.Dispose(), Times.Never);
    }

    [TestMethod]
    public void CommandExecutionIdleAdapter_CleanUpAsync_DisposesAllActiveTokens()
    {
        // Arrange
        TiVoCommand command = new TiVoCommand("Play", null, "Play", null, "Play", null, "Play");
        CommandExecutionIdleAdapter sut = MakeCommandAdapter(command);
        sut.InitializeAsync(Activity, CancellationToken.None);
        command.IsActive = true;

        // Act
        sut.CleanUpAsync(Activity, CancellationToken.None);

        // Assert
        MockToken.Verify(t => t.Dispose(), Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private ConversationIdleAdapter MakeConversationAdapter(ConversationView conversation)
    {
        Mock<IRemoteDefinitionService> mockDefinition = new();
        mockDefinition.SetupGet(d => d.RemoteRoot).Returns(new LayoutGroup("ROOT", [conversation]));
        return new(mockDefinition.Object, MockIdleDetector.Object);
    }

    private ProgrammingModeIdleAdapter MakeProgrammingAdapter(LifecycleView view)
        => new(view, MockIdleDetector.Object);

    private CommandExecutionIdleAdapter MakeCommandAdapter(Command command)
    {
        Mock<IRemoteDefinitionService> mockDefinition = new();
        mockDefinition.SetupGet(d => d.RemoteRoot).Returns(new LayoutGroup("ROOT", [command]));
        return new(mockDefinition.Object, MockIdleDetector.Object);
    }
}
