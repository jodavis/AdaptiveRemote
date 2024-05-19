using AdaptiveRemote.Logging;
using AdaptiveRemote.TestUtilities;
using Moq;

namespace AdaptiveRemote.Services.Conversation;

[TestClass]
public class ListeningControllerTests
{
    private readonly Mock<ISpeechRecognitionEngine> MockEngine = new();
    private readonly MockLogger<ListeningController> MockLogger = new();

    private IListeningController CreateSut() => new ListeningController(MockEngine.Object, MockLogger);

    private static string Expect_State(bool listening, int listenCount, int pauseCount = 0)
        => $"Information[501]: {string.Format(LoggingMessages.ListeningController_State, listening, listenCount, pauseCount)}";

    [TestInitialize]
    public void SetupMocks()
    {
        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Never);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Never);
    }

    [TestCleanup]
    public void ValidateMocks()
    {
        MockEngine.Verify();
    }

    [TestMethod]
    public void ListeningController_Listen_CallsRecognizeAsync()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        // Act
        IDisposable disposable = sut.Listen();

        // Assert
        Assert.IsNotNull(disposable, nameof(disposable));

        MockLogger.VerifyMessages(
            Expect_State(true, 1));
    }

    [TestMethod]
    public void ListeningController_ListenTwice_CallsRecognizeAsyncOnce()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        sut.Listen();

        // Act
        IDisposable disposable = sut.Listen();

        // Assert
        Assert.IsNotNull(disposable, nameof(disposable));

        MockLogger.VerifyMessages(
            Expect_State(true, 1),
            Expect_State(true, 2));
    }

    [TestMethod]
    public void ListeningController_Listen_Dispose_CallsRecognizeAsyncCancel()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        IDisposable disposable = sut.Listen();

        // Act
        disposable.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            Expect_State(true, 1),
            Expect_State(false, 0));
    }

    [TestMethod]
    public void ListeningController_ListenTwice_DisposeOnce_DoesNotCallRecognizeAsyncCancel()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        sut.Listen();
        IDisposable disposable = sut.Listen();

        // Act
        disposable.Dispose();

        // Assert
        MockLogger.VerifyMessages(
          Expect_State(true, 1),
          Expect_State(true, 2),
          Expect_State(true, 1));
    }

    [TestMethod]
    public void ListeningController_ListenTwice_DisposeInOrder_CallsRecognizeAsyncCancel()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        IDisposable disposable1 = sut.Listen();
        IDisposable disposable2 = sut.Listen();
        disposable1.Dispose();

        // Act
        disposable2.Dispose();

        // Assert
        MockLogger.VerifyMessages(
          Expect_State(true, 1),
          Expect_State(true, 2),
          Expect_State(true, 1),
          Expect_State(false, 0));
    }

    [TestMethod]
    public void ListeningController_ListenTwice_DisposeInReverse_CallsRecognizeAsyncCancel()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        IDisposable disposable1 = sut.Listen();
        IDisposable disposable2 = sut.Listen();
        disposable2.Dispose();

        // Act
        disposable1.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            Expect_State(true, 1),
            Expect_State(true, 2),
            Expect_State(true, 1),
            Expect_State(false, 0));
    }

    [TestMethod]
    public void ListeningController_ListenTwice_DisposeTwiceOnSameObject_DoesNotCallRecognizeAsyncCancel()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        sut.Listen();
        IDisposable disposable = sut.Listen();
        disposable.Dispose();

        // Act
        disposable.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            Expect_State(true, 1),
            Expect_State(true, 2),
            Expect_State(true, 1));
    }

    [TestMethod]
    public void ListeningController_Pause_WhenNotListening_DoesNothing()
    {
        // Arrange
        IListeningController sut = CreateSut();

        // Act
        IDisposable pause = sut.Pause();

        // Assert
        Assert.IsNotNull(pause, nameof(pause));

        MockLogger.VerifyMessages(
            Expect_State(false, 0, 1));
    }

    [TestMethod]
    public void ListeningController_Pause_Dispose_WhenNotListening_DoesNothing()
    {
        // Arrange
        IListeningController sut = CreateSut();

        IDisposable pause = sut.Pause();

        // Act
        pause.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            Expect_State(false, 0, 1),
            Expect_State(false, 0, 0));
    }

    [TestMethod]
    public void ListeningController_Pause_WhenListening_CallsRecognizeAsyncCanceled()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        sut.Listen();

        // Act
        IDisposable pause = sut.Pause();

        // Assert
        Assert.IsNotNull(pause, nameof(pause));

        MockLogger.VerifyMessages(
            Expect_State(true, 1, 0),
            Expect_State(false, 1, 1));
    }

    [TestMethod]
    public void ListeningController_PauseTwice_WhenListening_CallsRecognizeAsyncCanceled()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        sut.Listen();
        sut.Pause();

        // Act
        IDisposable pause = sut.Pause();

        // Assert
        Assert.IsNotNull(pause, nameof(pause));

        MockLogger.VerifyMessages(
            Expect_State(true, 1, 0),
            Expect_State(false, 1, 1),
            Expect_State(false, 1, 2));
    }

    [TestMethod]
    public void ListeningController_Pause_Dispose_WhenListening_CallsRecognizeAsync()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Exactly(2));
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        sut.Listen();
        IDisposable pause = sut.Pause();

        // Act
        pause.Dispose();

        // Assert
        Assert.IsNotNull(pause, nameof(pause));

        MockLogger.VerifyMessages(
            Expect_State(true, 1, 0),
            Expect_State(false, 1, 1),
            Expect_State(true, 1, 0));
    }

    [TestMethod]
    public void ListeningController_Pause_DisposeTwice_WhenListening_CallsRecognizeAsync()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Exactly(2));
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        sut.Listen();
        IDisposable pause = sut.Pause();
        pause.Dispose();

        // Act
        pause.Dispose();

        // Assert
        Assert.IsNotNull(pause, nameof(pause));

        MockLogger.VerifyMessages(
            Expect_State(true, 1, 0),
            Expect_State(false, 1, 1),
            Expect_State(true, 1, 0));
    }

    [TestMethod]
    public void ListeningController_PauseTwice_DisposeOnce_WhenListening_DoesNotCallRecognizeAsync()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        sut.Listen();
        IDisposable pause = sut.Pause();
        sut.Pause();

        // Act
        pause.Dispose();

        // Assert
        Assert.IsNotNull(pause, nameof(pause));

        MockLogger.VerifyMessages(
            Expect_State(true, 1, 0),
            Expect_State(false, 1, 1),
            Expect_State(false, 1, 2),
            Expect_State(false, 1, 1));
    }

    [TestMethod]
    public void ListeningController_PauseTwice_DisposeTwice_WhenListening_CallsRecognizeAsync()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Exactly(2));
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        sut.Listen();
        IDisposable pause1 = sut.Pause();
        IDisposable pause2 = sut.Pause();

        pause1.Dispose();

        // Act
        pause2.Dispose();

        // Assert
        Assert.IsNotNull(pause2, nameof(pause2));

        MockLogger.VerifyMessages(
            Expect_State(true, 1, 0),
            Expect_State(false, 1, 1),
            Expect_State(false, 1, 2),
            Expect_State(false, 1, 1),
            Expect_State(true, 1, 0));
    }

    [TestMethod]
    public void ListeningController_PauseTwice_DisposeTwiceOnSameObject_WhenListening_DoesNotCallRecognizeAsync()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        sut.Listen();
        IDisposable pause1 = sut.Pause();
        IDisposable pause2 = sut.Pause();

        pause1.Dispose();

        // Act
        pause1.Dispose();

        // Assert
        Assert.IsNotNull(pause2, nameof(pause2));

        MockLogger.VerifyMessages(
            Expect_State(true, 1, 0),
            Expect_State(false, 1, 1),
            Expect_State(false, 1, 2),
            Expect_State(false, 1, 1));
    }

    [TestMethod]
    public void ListeningController_ListenPauseStopListeningUnpause_CallsRecognizeAndCancelOnce()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        // Act
        IDisposable listen = sut.Listen();
        IDisposable pause = sut.Pause();
        listen.Dispose();
        pause.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            Expect_State(true, 1, 0),
            Expect_State(false, 1, 1),
            Expect_State(false, 0, 1),
            Expect_State(false, 0, 0));
    }

    [TestMethod]
    public void ListeningController_Pause_Listen_DoesNotCallRecognizeAsync()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Never);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Never);

        // Act
        IDisposable pause = sut.Pause();
        IDisposable listen = sut.Listen();

        // Assert
        MockLogger.VerifyMessages(
            Expect_State(false, 0, 1),
            Expect_State(false, 1, 1));
    }

    [TestMethod]
    public void ListeningController_Pause_Listen_Unpause_CallsRecognizeAsync()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Never);

        // Act
        IDisposable pause = sut.Pause();
        IDisposable listen = sut.Listen();
        pause.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            Expect_State(false, 0, 1),
            Expect_State(false, 1, 1),
            Expect_State(true, 1, 0));
    }
}
