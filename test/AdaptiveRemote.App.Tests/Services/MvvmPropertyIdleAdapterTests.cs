using AdaptiveRemote.Mvvm;
using FluentAssertions;

namespace AdaptiveRemote.Services;

[TestClass]
public class MvvmPropertyIdleAdapterTests
{
    [TestMethod]
    public void LastActivityTime_InitiallyMinValue_WhenPropertyIsFalse()
    {
        // Arrange
        TestTarget target = new() { IsActive = false };
        TestIdleAdapter sut = new(target);

        // Act
        DateTime result = sut.LastActivityTime;

        // Assert
        result.Should().Be(DateTime.MinValue);
    }

    [TestMethod]
    public void LastActivityTime_Now_WhenPropertyIsTrue()
    {
        // Arrange
        TestTarget target = new() { IsActive = true };
        TestIdleAdapter sut = new(target);

        // Act
        DateTime result = sut.LastActivityTime;

        // Assert
        result.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public void LastActivityTime_Updates_WhenPropertyChangesToFalse()
    {
        // Arrange
        TestTarget target = new() { IsActive = true };
        TestIdleAdapter sut = new(target);
        DateTime before = DateTime.Now.AddSeconds(-1);

        // Act
        target.IsActive = false;
        DateTime result = sut.LastActivityTime;

        // Assert
        result.Should().BeOnOrAfter(before);
    }

    [TestMethod]
    public void LastActivityTime_Resets_WhenPropertyChangesToTrue()
    {
        // Arrange
        TestTarget target = new() { IsActive = false };
        TestIdleAdapter sut = new(target);

        // Act
        target.IsActive = true;
        DateTime result = sut.LastActivityTime;

        // Assert
        result.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public void Dispose_Unsubscribes_PropertyChanged()
    {
        // Arrange
        TestTarget target = new() { IsActive = false };
        TestIdleAdapter sut = new(target);

        // Act
        sut.Dispose();

        // Assert
        target.IsActive = true;
        // Should not throw or update after dispose
        sut.LastActivityTime.Should().Be(DateTime.MinValue);
    }

    [TestMethod]
    public void Throws_OnNullTarget()
    {
        // Arrange
        Action act = () => _ = new TestIdleAdapter(null!);

        // Act & Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Throws_OnNullProperty()
    {
        // Arrange
        TestTarget target = new();
        Action act = () => _ = new BrokenIdleAdapter(target);

        // Act & Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // Helpers
    internal class TestTarget : MvvmObject
    {
        internal static readonly MvvmProperty<bool> IsActiveProperty = new(nameof(IsActive));
        internal bool IsActive
        {
            get => GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }
    }

    private sealed class TestIdleAdapter : MvvmPropertyIdleAdapter
    {
        public TestIdleAdapter(TestTarget target)
            : base(target, TestTarget.IsActiveProperty) { }
    }

    private sealed class BrokenIdleAdapter : MvvmPropertyIdleAdapter
    {
        public BrokenIdleAdapter(TestTarget target)
            : base(target, null!) { }
    }
}
