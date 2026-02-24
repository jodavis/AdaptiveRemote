using AdaptiveRemote.Models;
using FluentAssertions;

namespace AdaptiveRemote.Services.ModalMessages;

[TestClass]
public class ModalMessageServiceTests
{
    private static ModalMessageService CreateSut() => new();

    // ─── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Returns a body that waits until <paramref name="tcs"/> is completed.</summary>
    private static Func<CancellationToken, Task> SlowBody(TaskCompletionSource tcs)
        => _ => tcs.Task;

    /// <summary>Returns a body that completes immediately.</summary>
    private static Func<CancellationToken, Task> InstantBody()
        => _ => Task.CompletedTask;

    // ─── Queuing ────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ShowMessageAsync_SingleMessage_SetsAndClearsCurrentMessageAsync()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        string? messageWhileRunning = null;

        // Act
        await sut.ShowMessageAsync("Hello", async _ =>
        {
            messageWhileRunning = sut.View.CurrentMessage;
            await Task.CompletedTask;
        });

        // Assert
        messageWhileRunning.Should().Be("Hello", because: "message should be visible while body runs");
        sut.View.CurrentMessage.Should().BeNull(because: "message should be cleared after body completes");
    }

    [TestMethod]
    public async Task ShowMessageAsync_TwoMessages_SecondMessageWaitsForFirstAsync()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        TaskCompletionSource firstBodyTcs = new();
        TaskCompletionSource firstBodyStartedTcs = new();
        List<string> order = [];

        // Act – start first message (body will block until signalled)
        Task first = sut.ShowMessageAsync("First", async _ =>
        {
            firstBodyStartedTcs.SetResult(); // signal that body has started (and CurrentMessage is set)
            order.Add("first-body-start");
            await firstBodyTcs.Task;
            order.Add("first-body-end");
        });

        // Wait for first body to start so View.CurrentMessage is reliably set
        await firstBodyStartedTcs.Task;

        // Start second message (it should queue behind the first)
        Task second = sut.ShowMessageAsync("Second", _ =>
        {
            order.Add("second-body");
            return Task.CompletedTask;
        });

        // Neither task should complete yet
        first.IsCompleted.Should().BeFalse(because: "first body has not finished");
        second.IsCompleted.Should().BeFalse(because: "second is still queued behind first");
        sut.View.CurrentMessage.Should().Be("First", because: "first message is being displayed");

        // Unblock the first body
        firstBodyTcs.SetResult();
        await first;
        await second;

        // Assert ordering
        order.Should().HaveCount(3, because: "all three body steps should have run");
        order[0].Should().Be("first-body-start");
        order[1].Should().Be("first-body-end");
        order[2].Should().Be("second-body", because: "messages should be processed in FIFO order");
        sut.View.CurrentMessage.Should().BeNull(because: "both messages have been cleared");
    }

    [TestMethod]
    public async Task ShowMessageAsync_BodyThrows_ClearsMessageAndFaultsTaskAsync()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        InvalidOperationException expectedException = new("test error");

        // Act
        Task showTask = sut.ShowMessageAsync("Error message", _ => throw expectedException);
        Exception? caught = null;
        try
        {
            await showTask;
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        // Assert
        caught.Should().BeSameAs(expectedException);
        sut.View.CurrentMessage.Should().BeNull(because: "message should be cleared even when body throws");
    }

    [TestMethod]
    public async Task ShowMessageAsync_BodyThrows_SubsequentMessageStillShowsAsync()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        string? secondMessageSeen = null;

        // Act
        try
        {
            await sut.ShowMessageAsync("Error", _ => throw new InvalidOperationException());
        }
        catch (InvalidOperationException)
        {
        }

        await sut.ShowMessageAsync("Second", async _ =>
        {
            secondMessageSeen = sut.View.CurrentMessage;
            await Task.CompletedTask;
        });

        // Assert
        secondMessageSeen.Should().Be("Second", because: "the queue should continue processing after a faulted body");
        sut.View.CurrentMessage.Should().BeNull();
    }

    // ─── Markdown content preservation ──────────────────────────────────────────

    [TestMethod]
    public async Task ShowMessageAsync_MarkdownContent_PreservedVerbatimAsync()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        string markdownMessage = "# Title\n**bold** and _italic_";
        string? capturedMessage = null;

        // Act
        await sut.ShowMessageAsync(markdownMessage, async _ =>
        {
            capturedMessage = sut.View.CurrentMessage;
            await Task.CompletedTask;
        });

        // Assert
        capturedMessage.Should().Be(markdownMessage, because: "markdown is stored verbatim and rendered by the UI layer");
    }

    // ─── Keep-alive / message replacement ───────────────────────────────────────

    [TestMethod]
    public async Task ShowMessageAsync_KeepAlive_MessageRemainsAfterBodyCompletesAsync()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();

        // Act – show a keepAlive message and await its body completion
        await sut.ShowMessageAsync("Persistent", InstantBody(), keepAlive: true);

        // Assert: message should still be visible after body completes
        sut.View.CurrentMessage.Should().Be("Persistent", because: "keepAlive=true keeps the message visible after body completes");
    }

    [TestMethod]
    public async Task ShowMessageAsync_KeepAliveThenNewMessage_ReplacesOldMessageAsync()
    {
        // Arrange
        using ModalMessageService sut = CreateSut();
        string? firstMessageWhileSecondRuns = null;

        // Show a keepAlive message
        await sut.ShowMessageAsync("Persistent", InstantBody(), keepAlive: true);
        sut.View.CurrentMessage.Should().Be("Persistent");

        // Now show a new message – this should replace the keepAlive message
        await sut.ShowMessageAsync("Replacement", async _ =>
        {
            firstMessageWhileSecondRuns = sut.View.CurrentMessage;
            await Task.CompletedTask;
        });

        // Assert
        firstMessageWhileSecondRuns.Should().Be("Replacement",
            because: "the new message should replace the keepAlive message");
        sut.View.CurrentMessage.Should().BeNull(because: "replacement message was cleared after its body");
    }

    [TestMethod]
    public async Task ShowMessageAsync_KeepAlive_ViewModelRaisesPropertyChangedAsync()
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
        await sut.ShowMessageAsync("Hello", InstantBody());

        // Assert – message set then cleared
        observedMessages.Should().HaveCount(2, because: "PropertyChanged should fire when the message is set and when it is cleared");
        observedMessages[0].Should().Be("Hello");
        observedMessages[1].Should().BeNull();
    }
}
