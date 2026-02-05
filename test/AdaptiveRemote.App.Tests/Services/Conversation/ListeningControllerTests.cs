using Moq;

namespace AdaptiveRemote.Services.Conversation;

[TestClass]
public class ListeningControllerTests
{
    private readonly Mock<ISpeechRecognitionEngine> MockEngine = new();
    private readonly MockLogger<ListeningController> MockLogger = new();

    private IListeningController CreateSut() => new ListeningController(MockEngine.Object, MockLogger);

    [TestInitialize]
    public void SetupMocks()
    {
        MockEngine
            .Setup(x => x.Recognize())
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
    public void ListeningController_Listen_CallsRecognize()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
            .Verifiable(Times.Once);

        // Act
        IDisposable disposable = sut.Listen();

        // Assert
        Assert.IsNotNull(disposable, nameof(disposable));

        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
        });
    }

    [TestMethod]
    public void ListeningController_ListenTwice_CallsRecognizeAsyncOnce()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
            .Verifiable(Times.Once);

        sut.Listen();

        // Act
        IDisposable disposable = sut.Listen();

        // Assert
        Assert.IsNotNull(disposable, nameof(disposable));

        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(true, 2, 0);
        });
    }

    [TestMethod]
    public void ListeningController_Listen_Dispose_CallsRecognizeAsyncCancel()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        IDisposable disposable = sut.Listen();

        // Act
        disposable.Dispose();

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(false, 0, 0);
        });
    }

    [TestMethod]
    public void ListeningController_ListenTwice_DisposeOnce_DoesNotCallRecognizeAsyncCancel()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
            .Verifiable(Times.Once);

        sut.Listen();
        IDisposable disposable = sut.Listen();

        // Act
        disposable.Dispose();

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(true, 2, 0);
            log.ListeningController_State(true, 1, 0);
        });
    }

    [TestMethod]
    public void ListeningController_ListenTwice_DisposeInOrder_CallsRecognizeAsyncCancel()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
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
        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(true, 2, 0);
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(false, 0, 0);
        });
    }

    [TestMethod]
    public void ListeningController_ListenTwice_DisposeInReverse_CallsRecognizeAsyncCancel()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
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
        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(true, 2, 0);
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(false, 0, 0);
        });
    }

    [TestMethod]
    public void ListeningController_ListenTwice_DisposeTwiceOnSameObject_DoesNotCallRecognizeAsyncCancel()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
            .Verifiable(Times.Once);

        sut.Listen();
        IDisposable disposable = sut.Listen();
        disposable.Dispose();

        // Act
        disposable.Dispose();

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(true, 2, 0);
            log.ListeningController_State(true, 1, 0);
        });
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

        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(false, 0, 1);
        });
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
        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(false, 0, 1);
            log.ListeningController_State(false, 0, 0);
        });
    }

    [TestMethod]
    public void ListeningController_Pause_WhenListening_CallsRecognizeAsyncCanceled()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        sut.Listen();

        // Act
        IDisposable pause = sut.Pause();

        // Assert
        Assert.IsNotNull(pause, nameof(pause));

        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(false, 1, 1);
        });
    }

    [TestMethod]
    public void ListeningController_PauseTwice_WhenListening_CallsRecognizeAsyncCanceled()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
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

        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(false, 1, 1);
            log.ListeningController_State(false, 1, 2);
        });
    }

    [TestMethod]
    public void ListeningController_Pause_Dispose_WhenListening_CallsRecognize()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
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

        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(false, 1, 1);
            log.ListeningController_State(true, 1, 0);
        });
    }

    [TestMethod]
    public void ListeningController_Pause_DisposeTwice_WhenListening_CallsRecognize()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
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

        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(false, 1, 1);
            log.ListeningController_State(true, 1, 0);
        });
    }

    [TestMethod]
    public void ListeningController_PauseTwice_DisposeOnce_WhenListening_DoesNotCallRecognize()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
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

        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(false, 1, 1);
            log.ListeningController_State(false, 1, 2);
            log.ListeningController_State(false, 1, 1);
        });
    }

    [TestMethod]
    public void ListeningController_PauseTwice_DisposeTwice_WhenListening_CallsRecognize()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
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

        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(false, 1, 1);
            log.ListeningController_State(false, 1, 2);
            log.ListeningController_State(false, 1, 1);
            log.ListeningController_State(true, 1, 0);
        });
    }

    [TestMethod]
    public void ListeningController_PauseTwice_DisposeTwiceOnSameObject_WhenListening_DoesNotCallRecognize()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
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

        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(false, 1, 1);
            log.ListeningController_State(false, 1, 2);
            log.ListeningController_State(false, 1, 1);
        });
    }

    [TestMethod]
    public void ListeningController_ListenPauseStopListeningUnpause_CallsRecognizeAndCancelOnce()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
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
        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(false, 1, 1);
            log.ListeningController_State(false, 0, 1);
            log.ListeningController_State(false, 0, 0);
        });
    }

    [TestMethod]
    public void ListeningController_Pause_Listen_DoesNotCallRecognize()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
            .Verifiable(Times.Never);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Never);

        // Act
        IDisposable pause = sut.Pause();
        IDisposable listen = sut.Listen();

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(false, 0, 1);
            log.ListeningController_State(false, 1, 1);
        });
    }

    [TestMethod]
    public void ListeningController_Pause_Listen_Unpause_CallsRecognize()
    {
        // Arrange
        IListeningController sut = CreateSut();

        MockEngine
            .Setup(x => x.Recognize())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Never);

        // Act
        IDisposable pause = sut.Pause();
        IDisposable listen = sut.Listen();
        pause.Dispose();

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(false, 0, 1);
            log.ListeningController_State(false, 1, 1);
            log.ListeningController_State(true, 1, 0);
        });
    }

    [TestMethod]
    public void ListeningController_Listen_WithError_ThrowsAndDecreasesListenCount()
    {
        // Arrange
        IListeningController sut = CreateSut();

        Exception expectedException = new InvalidOperationException("Some thing didn't work");

        MockEngine
            .Setup(x => x.Recognize())
            .Throws(expectedException)
            .Verifiable(Times.Once);

        try
        {
            // Act
            sut.Listen();

            // Assert
            Assert.Fail("Expected exception was not thrown");
        }
        catch (InvalidOperationException result)
        {
            Assert.AreEqual(expectedException.Message, result.Message, nameof(result) + ".Message");
        }

        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_RecognizeAsyncError(expectedException);
            log.ListeningController_State(false, 0, 0);
        });
    }

    [TestMethod]
    public void ListeningController_Listen_Dispose_WithError_LogsErrorButDoesNotThrow()
    {
        // Arrange
        IListeningController sut = CreateSut();

        Exception expectedException = new InvalidOperationException("Some thing didn't work");

        MockEngine
            .Setup(x => x.Recognize())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Throws(expectedException)
            .Verifiable(Times.Once);

        IDisposable listen = sut.Listen();

        // Act
        listen.Dispose();

        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_RecognizeAsyncCancelError(expectedException);
            log.ListeningController_State(true, 0, 0);
        });
    }

    [TestMethod]
    public void ListeningController_Pause_WithError_ThrowsAndDecreasesPauseCount()
    {
        // Arrange
        IListeningController sut = CreateSut();

        Exception expectedException = new InvalidOperationException("Some thing didn't work");

        MockEngine
            .Setup(x => x.Recognize())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Throws(expectedException)
            .Verifiable(Times.Once);

        sut.Listen();

        try
        {
            // Act
            sut.Pause();

            // Assert
            Assert.Fail("Expected exception was not thrown");
        }
        catch (InvalidOperationException result)
        {
            Assert.AreEqual(expectedException.Message, result.Message, nameof(result) + ".Message");
        }

        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_RecognizeAsyncCancelError(expectedException);
            log.ListeningController_State(true, 1, 0);
        });
    }

    [TestMethod]
    public void ListeningController_Pause_Dispose_WithError_ThrowsAndDecreasesPauseCount()
    {
        // Arrange
        IListeningController sut = CreateSut();

        Exception expectedException = new InvalidOperationException("Some thing didn't work");

        MockEngine
            .Setup(x => x.Recognize())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        sut.Listen();
        IDisposable pause = sut.Pause();

        MockEngine
            .Setup(x => x.Recognize())
            .Throws(expectedException)
            .Verifiable(Times.Once);

        // Act
        pause.Dispose();

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ListeningController_State(true, 1, 0);
            log.ListeningController_State(false, 1, 1);
            log.ListeningController_RecognizeAsyncError(expectedException);
            log.ListeningController_State(false, 1, 0);
        });
    }
}
