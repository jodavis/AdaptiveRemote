using AdaptiveRemote.Models;
using FluentAssertions;

namespace AdaptiveRemote.Services.Lifecycle;

[TestClass]
public class LifecycleViewControllerTests
{
    private LifecycleView ViewModel { get; } = new();

    private LifecycleViewController CreateSut() => new(ViewModel);

    // ─── Constructor ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void LifecycleViewController_Constructor_SetsUpShowErrorCommand()
    {
        // Act
        LifecycleViewController sut = CreateSut();

        // Assert
        ViewModel.ShowErrorCommand.Should().NotBeNull();
        ViewModel.ShowError.Should().BeFalse();
        ViewModel.ShowErrorCommand.Execute(null);
        ViewModel.ShowError.Should().BeTrue(because: "ShowErrorCommand should set ShowError to true");
    }

    [TestMethod]
    public void LifecycleViewController_Constructor_SetsUpCloseErrorCommand()
    {
        // Act
        LifecycleViewController sut = CreateSut();
        ViewModel.ShowError = true;

        // Assert
        ViewModel.CloseErrorCommand.Should().NotBeNull();
        ViewModel.CloseErrorCommand.Execute(null);
        ViewModel.ShowError.Should().BeFalse(because: "CloseErrorCommand should set ShowError to false");
    }

    // ─── SetPhase ────────────────────────────────────────────────────────────────

    [TestMethod]
    [DataRow(LifecyclePhase.Starting, "Starting application")]
    [DataRow(LifecyclePhase.Building, "Building service graph")]
    [DataRow(LifecyclePhase.SettingUp, "Starting services")]
    [DataRow(LifecyclePhase.Stopping, "Stopping application")]
    [DataRow(LifecyclePhase.CleaningUp, "Cleaning up application")]
    [DataRow(LifecyclePhase.Ready, "")]
    public void SetPhase_SetsCurrentPhaseAndTaskName(LifecyclePhase phase, string expectedDescription)
    {
        // Arrange
        LifecycleViewController sut = CreateSut();

        // Act
        sut.SetPhase(phase);

        // Assert
        ViewModel.CurrentPhase.Should().Be(phase, because: "SetPhase should update the view model's CurrentPhase");

        if (string.IsNullOrEmpty(expectedDescription))
        {
            ViewModel.TaskName.Should().BeEmpty(because: "a phase with no description should produce an empty task name");
        }
        else
        {
            ViewModel.TaskName.Should().Be($"{expectedDescription}...", because: "SetPhase should update the TaskName to the phase description with ellipsis");
        }
    }

    // ─── StartTask ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void StartTask_ReturnsActivityWithDescription()
    {
        // Arrange
        LifecycleViewController sut = CreateSut();

        // Act
        ILifecycleActivity activity = sut.StartTask("Loading...");

        // Assert
        ViewModel.TaskName.Should().Be("Loading......", because: "StartTask should update the TaskName to the activity description");
        activity.Should().NotBeNull();
    }

    [TestMethod]
    public void StartTask_MultipleActivities_ShowsFirstStartedTask()
    {
        // Arrange
        LifecycleViewController sut = CreateSut();
        sut.SetPhase(LifecyclePhase.SettingUp);

        // Act – tasks are inserted at position 1, pushing older tasks toward the end of the list.
        // UpdateTaskName uses _activities[Count - 1] (the tail), so the first-started task is shown.
        ILifecycleActivity activity1 = sut.StartTask("First task");
        ILifecycleActivity activity2 = sut.StartTask("Second task");

        // Assert – first-started task stays at the tail
        ViewModel.TaskName.Should().Be("First task...", because: "the first-started task ends up at the tail of the list");

        // Dispose the second (at position 1) — first task remains at the tail
        activity2.Dispose();
        ViewModel.TaskName.Should().Be("First task...", because: "after disposing the second task, the first task is still at the tail");

        activity1.Dispose();
        ViewModel.TaskName.Should().Be("Starting services...", because: "after all tasks are disposed, the phase description is restored");
    }

    [TestMethod]
    public void StartTask_Activity_SettingDescription_UpdatesTaskName()
    {
        // Arrange
        LifecycleViewController sut = CreateSut();
        ILifecycleActivity activity = sut.StartTask("Initial");

        // Act
        activity.Description = "Updated";

        // Assert
        ViewModel.TaskName.Should().Be("Updated...", because: "changing Description should update the task name");
    }

    [TestMethod]
    public void StartTask_Activity_Dispose_RemovesFromTaskName()
    {
        // Arrange
        LifecycleViewController sut = CreateSut();
        sut.SetPhase(LifecyclePhase.SettingUp);
        ILifecycleActivity activity = sut.StartTask("In progress");

        // Act
        activity.Dispose();

        // Assert
        ViewModel.TaskName.Should().Be("Starting services...", because: "disposing the activity should restore the phase description");
    }

    // ─── SetFatalError ───────────────────────────────────────────────────────────

    [TestMethod]
    public void SetFatalError_SetsPhaseToFatalError()
    {
        // Arrange
        LifecycleViewController sut = CreateSut();

        // Act
        sut.SetFatalError(new InvalidOperationException("something went wrong"));

        // Assert
        ViewModel.CurrentPhase.Should().Be(LifecyclePhase.FatalError, because: "SetFatalError should transition the phase to FatalError");
    }

    [TestMethod]
    public void SetFatalError_SetsShowBrowserFalse_AndShowButtonPanelTrue()
    {
        // Arrange
        LifecycleViewController sut = CreateSut();

        // Act
        sut.SetFatalError(new InvalidOperationException("something went wrong"));

        // Assert
        ViewModel.ShowBrowser.Should().BeFalse(because: "SetFatalError should hide the browser");
        ViewModel.ShowButtonPanel.Should().BeTrue(because: "SetFatalError should show the button panel");
    }

    [TestMethod]
    public void SetFatalError_SingleLineMessage_SetsFatalErrorWithoutDetail()
    {
        // Arrange
        LifecycleViewController sut = CreateSut();
        Exception error = new InvalidOperationException("something went wrong");

        // Act
        sut.SetFatalError(error);

        // Assert
        ViewModel.FatalError.Should().NotBeNull();
        ViewModel.FatalErrorDetail.Should().BeNull(because: "a single-line exception should have no detail");
    }

    [TestMethod]
    public void SetFatalError_ViaActivity_SetsErrorPrefixOnActivity()
    {
        // Arrange
        LifecycleViewController sut = CreateSut();
        ILifecycleActivity activity = sut.StartTask("Connecting");

        // Act
        activity.SetFatalError(new InvalidOperationException("connection failed"));

        // Assert
        ViewModel.CurrentPhase.Should().Be(LifecyclePhase.FatalError, because: "SetFatalError via activity should transition the phase to FatalError");
        ViewModel.FatalError.Should().NotBeNull();
    }

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
        sut.EnterLearningMode();

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
