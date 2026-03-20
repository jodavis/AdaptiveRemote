using AdaptiveRemote.Models;
using FluentAssertions;

namespace AdaptiveRemote.Services.Lifecycle;

[TestClass]
public class LifecycleViewControllerTests
{
    private LifecycleView ViewModel { get; } = new();

    private LifecycleViewController CreateSut() => new(ViewModel);

    // ─── EnterLearningMode ───────────────────────────────────────────────────────

    [TestMethod]
    public void EnterLearningMode_WhenNotInLearningMode_SetsProgrammingModeAndReturnsToken()
    {
        // Arrange
        LifecycleViewController sut = CreateSut();

        // Act
        CancellationToken token = sut.EnterLearningMode();

        // Assert
        ViewModel.IsProgrammingMode.Should().BeTrue(because: "EnterLearningMode should activate programming mode");
        token.IsCancellationRequested.Should().BeFalse(because: "the new token should not be cancelled yet");
        ViewModel.LearningCancellationToken.Should().Be(token, because: "the view model should expose the same token");
    }

    [TestMethod]
    public void EnterLearningMode_CalledTwice_ReturnsSameToken()
    {
        // Arrange
        LifecycleViewController sut = CreateSut();

        // Act
        CancellationToken first = sut.EnterLearningMode();
        CancellationToken second = sut.EnterLearningMode();

        // Assert
        second.Should().Be(first, because: "a second call while already in learning mode should return the existing token, not create a new one");
        ViewModel.IsProgrammingMode.Should().BeTrue(because: "programming mode should still be active");
    }

    [TestMethod]
    public void EnterLearningMode_CalledTwice_DoesNotCancelFirstToken()
    {
        // Arrange
        LifecycleViewController sut = CreateSut();

        // Act
        CancellationToken first = sut.EnterLearningMode();
        CancellationToken second = sut.EnterLearningMode();

        // Assert
        first.IsCancellationRequested.Should().BeFalse(because: "a second EnterLearningMode call must not cancel the in-progress learning operation");
    }

    // ─── ExitLearningModeAsync ───────────────────────────────────────────────────

    [TestMethod]
    public void ExitLearningModeAsync_AfterEnterLearningMode_CancelsToken()
    {
        // Arrange
        LifecycleViewController sut = CreateSut();
        CancellationToken token = sut.EnterLearningMode();

        // Act
        Task exitTask = sut.ExitLearningModeAsync();

        // Assert
        exitTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));
        token.IsCancellationRequested.Should().BeTrue(because: "ExitLearningModeAsync should cancel the learning token");
        ViewModel.IsProgrammingMode.Should().BeFalse(because: "ExitLearningModeAsync should deactivate programming mode");
    }

    [TestMethod]
    public void ExitLearningModeAsync_WhenNotInLearningMode_CompletesWithoutError()
    {
        // Arrange
        LifecycleViewController sut = CreateSut();

        // Act
        Task exitTask = sut.ExitLearningModeAsync();

        // Assert
        exitTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "ExitLearningModeAsync should be a no-op when not in learning mode");
    }

    [TestMethod]
    public void EnterLearningMode_AfterExitLearningMode_CreatesNewToken()
    {
        // Arrange
        LifecycleViewController sut = CreateSut();
        CancellationToken first = sut.EnterLearningMode();
        sut.ExitLearningModeAsync();

        // Act – re-enter learning mode
        CancellationToken second = sut.EnterLearningMode();

        // Assert
        second.Should().NotBe(first, because: "re-entering learning mode after exiting should create a fresh token");
        second.IsCancellationRequested.Should().BeFalse(because: "the new token should not be cancelled");
        ViewModel.IsProgrammingMode.Should().BeTrue(because: "programming mode should be active again");
    }
}
