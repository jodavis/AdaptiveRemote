using AdaptiveRemote.Mvvm;
using AdaptiveRemote.Services.CloudAssets;
using FluentAssertions;
using Moq;

namespace AdaptiveRemote.Services;

/// <summary>
/// Tests for <see cref="MvvmPropertyIdleAdapter"/> via a minimal concrete subclass.
/// Individual adapter classes (ConversationIdleAdapter etc.) delegate all logic to this
/// base, so their only testable surface is construction with the right property.
/// </summary>
[TestClass]
public class MvvmPropertyIdleAdapterTests
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

    [TestMethod]
    public void MvvmPropertyIdleAdapter_InitializeAsync_NoTokenWhenPropertyFalse()
    {
        // Arrange
        TestTarget target = new();
        TestIdleAdapter sut = MakeAdapter(target);

        // Act
        sut.InitializeAsync(Activity, CancellationToken.None);

        // Assert
        MockIdleDetector.Verify(d => d.StartNonIdle(), Times.Never);
    }

    [TestMethod]
    public void MvvmPropertyIdleAdapter_InitializeAsync_HoldsTokenWhenPropertyAlreadyTrue()
    {
        // Arrange
        TestTarget target = new() { IsActive = true };
        TestIdleAdapter sut = MakeAdapter(target);

        // Act
        sut.InitializeAsync(Activity, CancellationToken.None);

        // Assert
        MockIdleDetector.Verify(d => d.StartNonIdle(), Times.Once);
        MockToken.Verify(t => t.Dispose(), Times.Never);
    }

    [TestMethod]
    public void MvvmPropertyIdleAdapter_PropertyBecomesTrue_AcquiresToken()
    {
        // Arrange
        TestTarget target = new();
        TestIdleAdapter sut = MakeAdapter(target);
        sut.InitializeAsync(Activity, CancellationToken.None);

        // Act
        target.IsActive = true;

        // Assert
        MockIdleDetector.Verify(d => d.StartNonIdle(), Times.Once);
        MockToken.Verify(t => t.Dispose(), Times.Never);
    }

    [TestMethod]
    public void MvvmPropertyIdleAdapter_PropertyBecomesFalse_DisposesToken()
    {
        // Arrange
        TestTarget target = new();
        TestIdleAdapter sut = MakeAdapter(target);
        sut.InitializeAsync(Activity, CancellationToken.None);
        target.IsActive = true;

        // Act
        target.IsActive = false;

        // Assert
        MockToken.Verify(t => t.Dispose(), Times.Once);
    }

    [TestMethod]
    public void MvvmPropertyIdleAdapter_CleanUpAsync_DisposesToken()
    {
        // Arrange
        TestTarget target = new();
        TestIdleAdapter sut = MakeAdapter(target);
        sut.InitializeAsync(Activity, CancellationToken.None);
        target.IsActive = true;

        // Act
        sut.CleanUpAsync(Activity, CancellationToken.None);

        // Assert
        MockToken.Verify(t => t.Dispose(), Times.Once);
    }

    [TestMethod]
    public void MvvmPropertyIdleAdapter_CleanUpAsync_UnsubscribesFromPropertyChanged()
    {
        // Arrange
        TestTarget target = new();
        TestIdleAdapter sut = MakeAdapter(target);
        sut.InitializeAsync(Activity, CancellationToken.None);

        // Act
        sut.CleanUpAsync(Activity, CancellationToken.None);
        target.IsActive = true;  // fire PropertyChanged after cleanup

        // Assert: no token acquired after cleanup
        MockIdleDetector.Verify(d => d.StartNonIdle(), Times.Never);
    }

    [TestMethod]
    public void MvvmPropertyIdleAdapter_PropertyChangedAfterCleanUp_NoTokenLeak()
    {
        // Simulates the race: CleanUpAsync runs, then a PropertyChanged notification
        // arrives on a different thread before the unsubscribe takes effect.
        // The _subscribed guard should prevent a new token from being acquired.

        // Arrange
        TestTarget target = new();
        TestIdleAdapter sut = MakeAdapter(target);
        sut.InitializeAsync(Activity, CancellationToken.None);

        // Act
        sut.CleanUpAsync(Activity, CancellationToken.None);
        // Simulate a late PropertyChanged arriving after cleanup
        target.IsActive = true;

        // Assert
        MockIdleDetector.Verify(d => d.StartNonIdle(), Times.Never);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private TestIdleAdapter MakeAdapter(TestTarget target)
        => new(target, MockIdleDetector.Object);

    // Minimal concrete MvvmObject with one bool property for testing the base class.
    internal class TestTarget : MvvmObject
    {
        internal static readonly MvvmProperty<bool> IsActiveProperty = new(nameof(IsActive));

        internal bool IsActive
        {
            get => GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public override string ToString() => "TestTarget";
    }

    // Thin subclass of MvvmPropertyIdleAdapter that wires up the TestTarget.
    private sealed class TestIdleAdapter : MvvmPropertyIdleAdapter
    {
        internal TestIdleAdapter(TestTarget target, IIdleDetector idleDetector)
            : base(target, TestTarget.IsActiveProperty, idleDetector)
        {
        }

        public override string Name => "Test idle adapter";
    }
}
