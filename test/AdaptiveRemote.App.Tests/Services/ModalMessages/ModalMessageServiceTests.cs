using AdaptiveRemote.Models;
using FluentAssertions;

namespace AdaptiveRemote.Services.ModalMessages;

[TestClass]
public class ModalMessageServiceTests
{
    private readonly ModalMessageView View = new();

    private ModalMessageService CreateSut() => new(View);

    // ─── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Returns a body that waits until <paramref name="tcs"/> is completed.</summary>
    private static Func<CancellationToken, Task> SlowBody(TaskCompletionSource tcs)
        => _ => tcs.Task;

    /// <summary>Returns a body that completes immediately.</summary>
    private static Func<CancellationToken, Task> InstantBody()
        => _ => Task.CompletedTask;

    // ─── Single message – set / clear lifecycle ──────────────────────────────────

    [TestMethod]
    public void ShowMessageAsync_SingleMessage_SetsCurrentMessage()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        string? messageWhileRunning = null;

        // Act
        Task showTask = sut.ShowMessageAsync("Hello", _ =>
        {
            messageWhileRunning = sut.View.CurrentMessage;
            return Task.CompletedTask;
        });

        showTask.Should().BeCompleteWithin(TimeSpan.FromMilliseconds(100), because: "instant body should complete quickly");

        // Assert
        messageWhileRunning.Should().Be("Hello", because: "message should be visible while body runs");
        sut.View.CurrentMessage.Should().BeNull(because: "message should be cleared after body completes");
    }

    [TestMethod]
    public void ShowMessageAsync_WhileBodyRunning_TaskIsNotComplete()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        TaskCompletionSource bodySlow = new();
        TaskCompletionSource bodyStarted = new();

        // Act
        Task showTask = sut.ShowMessageAsync("Hello", _ =>
        {
            bodyStarted.SetResult();
            return bodySlow.Task;
        });

        // Wait for background processor to pick up the request and start the body
        bodyStarted.Task.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "body should start promptly");

        // While body is still running, the task must not be complete and the message must be set
        showTask.Should().NotBeComplete(because: "ShowMessageAsync should not complete until the body finishes");
        sut.View.CurrentMessage.Should().Be("Hello", because: "message should remain visible while body runs");

        // Unblock the body
        bodySlow.SetResult();

        showTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "task should complete after body finishes");
        sut.View.CurrentMessage.Should().BeNull(because: "message should be cleared after body completes");
    }

    // ─── Queuing (FIFO) ──────────────────────────────────────────────────────────

    [TestMethod]
    public void ShowMessageAsync_TwoMessages_SecondMessageWaitsForFirst()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        TaskCompletionSource firstBodyUnblock = new();
        TaskCompletionSource firstBodyStarted = new();
        List<string> order = [];

        // Start first message (body blocks until signalled)
        Task first = sut.ShowMessageAsync("First", async _ =>
        {
            firstBodyStarted.SetResult();
            order.Add("first-body-start");
            await firstBodyUnblock.Task;
            order.Add("first-body-end");
        });

        // Wait for first body to start so CurrentMessage is reliably set
        firstBodyStarted.Task.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Enqueue second message while first is still running
        Task second = sut.ShowMessageAsync("Second", _ =>
        {
            order.Add("second-body");
            return Task.CompletedTask;
        });

        // Neither task should complete yet
        first.Should().NotBeComplete(because: "first body has not finished");
        second.Should().NotBeComplete(because: "second is still queued behind first");
        sut.View.CurrentMessage.Should().Be("First", because: "first message is being displayed");

        // Unblock first body
        firstBodyUnblock.SetResult();

        first.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));
        second.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Assert FIFO ordering
        order.Should().HaveCount(3, because: "all three steps should have run");
        order[0].Should().Be("first-body-start");
        order[1].Should().Be("first-body-end");
        order[2].Should().Be("second-body", because: "messages should be processed in FIFO order");
        sut.View.CurrentMessage.Should().BeNull(because: "both messages have been cleared");
    }

    // ─── Fault handling ──────────────────────────────────────────────────────────

    [TestMethod]
    public void ShowMessageAsync_BodyThrows_ClearsMessageAndFaultsTask()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        InvalidOperationException expectedException = new("test error");

        // Act
        Task showTask = sut.ShowMessageAsync("Error message", _ => throw expectedException);

        // Assert
        showTask.Should().BeFaultedWith(expectedException, TimeSpan.FromMilliseconds(100));
        sut.View.CurrentMessage.Should().BeNull(because: "message should be cleared even when body throws");
    }

    [TestMethod]
    public void ShowMessageAsync_BodyThrows_SubsequentMessageStillShows()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        InvalidOperationException faultException = new("error");
        string? secondMessageSeen = null;

        // Act – first message faults
        Task first = sut.ShowMessageAsync("Error", _ => throw faultException);
        first.Should().BeFaultedWith(faultException, TimeSpan.FromMilliseconds(100));

        // Second message should still be processed
        Task second = sut.ShowMessageAsync("Second", _ =>
        {
            secondMessageSeen = sut.View.CurrentMessage;
            return Task.CompletedTask;
        });
        second.Should().BeCompleteWithin(TimeSpan.FromMilliseconds(100), because: "the queue should continue processing after a faulted body");

        // Assert
        secondMessageSeen.Should().Be("Second");
        sut.View.CurrentMessage.Should().BeNull();
    }

    // ─── Markdown content preservation ──────────────────────────────────────────

    [TestMethod]
    public void ShowMessageAsync_MarkdownContent_PreservedVerbatim()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        string markdownMessage = "# Title\n**bold** and _italic_";
        string? capturedMessage = null;

        // Act
        Task showTask = sut.ShowMessageAsync(markdownMessage, _ =>
        {
            capturedMessage = sut.View.CurrentMessage;
            return Task.CompletedTask;
        });

        showTask.Should().BeCompleteWithin(TimeSpan.FromMilliseconds(100));

        // Assert
        capturedMessage.Should().Be(markdownMessage, because: "markdown is stored verbatim and rendered by the UI layer");
    }

    // ─── Keep-alive / message replacement ───────────────────────────────────────

    [TestMethod]
    public void ShowMessageAsync_KeepAlive_MessageRemainsAfterBodyCompletes()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();

        // Act – show a keepAlive message and wait for it to complete
        Task showTask = sut.ShowMessageAsync("Persistent", InstantBody(), keepAlive: true);

        showTask.Should().BeCompleteWithin(TimeSpan.FromMilliseconds(100));

        // Assert: message should still be visible after body completes
        sut.View.CurrentMessage.Should().Be("Persistent", because: "keepAlive=true keeps the message visible after body completes");
    }

    [TestMethod]
    public void ShowMessageAsync_KeepAliveThenNewMessage_ReplacesOldMessage()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        string? messageWhileSecondRuns = null;

        // Show a keepAlive message
        Task first = sut.ShowMessageAsync("Persistent", InstantBody(), keepAlive: true);
        first.Should().BeCompleteWithin(TimeSpan.FromMilliseconds(100));
        sut.View.CurrentMessage.Should().Be("Persistent");

        // Show a new message – this should replace the keepAlive message
        Task second = sut.ShowMessageAsync("Replacement", _ =>
        {
            messageWhileSecondRuns = sut.View.CurrentMessage;
            return Task.CompletedTask;
        });
        second.Should().BeCompleteWithin(TimeSpan.FromMilliseconds(100));

        // Assert
        messageWhileSecondRuns.Should().Be("Replacement",
            because: "the new message should replace the keepAlive message");
        sut.View.CurrentMessage.Should().BeNull(because: "replacement message was cleared after its body");
    }

    // ─── PropertyChanged notification ────────────────────────────────────────────

    [TestMethod]
    public void ShowMessageAsync_ViewModelRaisesPropertyChanged()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        List<string?> observedMessages = [];
        sut.View.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ModalMessageView.CurrentMessage))
            {
                observedMessages.Add(sut.View.CurrentMessage);
            }
        };

        // Act
        Task showTask = sut.ShowMessageAsync("Hello", InstantBody());
        showTask.Should().BeCompleteWithin(TimeSpan.FromMilliseconds(100));

        // Assert – PropertyChanged fires once when set and once when cleared
        observedMessages.Should().HaveCount(2, because: "PropertyChanged should fire when the message is set and when it is cleared");
        observedMessages[0].Should().Be("Hello");
        observedMessages[1].Should().BeNull();
    }

    // ─── Cancellation ────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShowMessageAsync_Cancellation_TokenIsPassedToBody()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        using CancellationTokenSource cts = new();
        CancellationToken? capturedToken = null;
        TaskCompletionSource bodyStarted = new();
        TaskCompletionSource bodySlow = new();

        // Act – body captures the token and waits
        Task showTask = sut.ShowMessageAsync("Hello", ct =>
        {
            capturedToken = ct;
            bodyStarted.SetResult();
            return bodySlow.Task;
        }, cancellationToken: cts.Token);

        bodyStarted.Task.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // The token passed to the body should not be cancelled yet
        capturedToken.Should().NotBeNull();
        capturedToken!.Value.IsCancellationRequested.Should().BeFalse(because: "token should not be cancelled yet");

        // Cancel and verify the token is observed
        cts.Cancel();
        capturedToken.Value.IsCancellationRequested.Should().BeTrue(because: "body should observe the cancellation");

        // Simulate body honouring cancellation
        bodySlow.SetCanceled(cts.Token);

        showTask.Should().BeCanceledWithin(TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public void ShowMessageAsync_Cancellation_ClearsMessageAndReturnsCancelled()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        using CancellationTokenSource cts = new();
        TaskCompletionSource bodyStarted = new();
        TaskCompletionSource bodySlow = new();

        // Act
        Task showTask = sut.ShowMessageAsync("Hello", _ =>
        {
            bodyStarted.SetResult();
            return bodySlow.Task;
        }, cancellationToken: cts.Token);

        bodyStarted.Task.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));
        sut.View.CurrentMessage.Should().Be("Hello", because: "message should be visible while body runs");

        // Cancel and let body throw OperationCanceledException
        cts.Cancel();
        bodySlow.SetCanceled(cts.Token);

        // Task should be in Canceled state and message should be cleared
        showTask.Should().BeCanceledWithin(TimeSpan.FromSeconds(1));
        sut.View.CurrentMessage.Should().BeNull(because: "message should be cleared after cancellation");
    }

    [TestMethod]
    public void ShowMessageAsync_Cancellation_SubsequentMessageStillShows()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        using CancellationTokenSource cts = new();
        string? secondMessageSeen = null;

        // Cancel the first message's token before submitting
        cts.Cancel();

        Task first = sut.ShowMessageAsync("First", ct =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }, cancellationToken: cts.Token);

        first.Should().BeCanceledWithin(TimeSpan.FromMilliseconds(100));

        // Second message should still be processed normally
        Task second = sut.ShowMessageAsync("Second", _ =>
        {
            secondMessageSeen = sut.View.CurrentMessage;
            return Task.CompletedTask;
        });

        second.Should().BeCompleteWithin(TimeSpan.FromMilliseconds(100), because: "the queue should continue after a cancelled body");
        secondMessageSeen.Should().Be("Second");
        sut.View.CurrentMessage.Should().BeNull();
    }

    // ─── Dispose behavior ────────────────────────────────────────────────────────

    [TestMethod]
    public void ShowMessageAsync_AfterDispose_FaultsWithChannelClosedException()
    {
        // Arrange
        ModalMessageService sut = CreateSut();
        sut.Dispose();

        // Act
        Task showTask = sut.ShowMessageAsync("Hello", _ => Task.CompletedTask);

        // Assert – channel is complete so WriteAsync throws ChannelClosedException
        showTask.Should().BeFaultedWith(
            new System.Threading.Channels.ChannelClosedException(),
            TimeSpan.FromMilliseconds(100),
            because: "ShowMessageAsync should fault when the channel is closed after Dispose");
    }
}
