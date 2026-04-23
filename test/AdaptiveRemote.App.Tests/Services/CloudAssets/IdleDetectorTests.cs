using FluentAssertions;

namespace AdaptiveRemote.Services.CloudAssets;

[TestClass]
public class IdleDetectorTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static IdleDetector MakeSut(int cooldownSeconds = 0)
        => new(new MockOptions<CloudSettings>(new() { IdleCooldownSeconds = cooldownSeconds }));

    [TestMethod]
    public void IdleDetector_IsIdle_FalseByDefault()
    {
        // Construction schedules the initial cooldown; system is not idle yet.
        IdleDetector sut = MakeSut(cooldownSeconds: 30);

        sut.IsIdle.Should().BeFalse();
    }

    [TestMethod]
    public void IdleDetector_Construction_BecomesIdleAfterInitialCooldown()
    {
        // Arrange
        IdleDetector sut = MakeSut(cooldownSeconds: 0);
        TaskCompletionSource becameIdle = new();
        sut.BecameIdle += (_, _) => becameIdle.TrySetResult();

        // Handle the race where the timer fires before we subscribed.
        if (sut.IsIdle)
        {
            becameIdle.TrySetResult();
        }

        // Assert
        becameIdle.Task.Should().BeCompleteWithin(Timeout);
        sut.IsIdle.Should().BeTrue();
    }

    [TestMethod]
    public void IdleDetector_StartNonIdle_IsIdleFalseWhileTokenHeld()
    {
        // Arrange
        IdleDetector sut = MakeSut();

        // Act
        using IDisposable token = sut.StartNonIdle();

        // Assert
        sut.IsIdle.Should().BeFalse();
    }

    [TestMethod]
    public void IdleDetector_DisposeLastToken_BecameIdleFiresAfterCooldown()
    {
        // Arrange
        IdleDetector sut = MakeSut(cooldownSeconds: 0);
        TaskCompletionSource becameIdle = new();
        sut.BecameIdle += (_, _) => becameIdle.TrySetResult();

        // Act
        IDisposable token = sut.StartNonIdle();
        token.Dispose();

        // Assert
        becameIdle.Task.Should().BeCompleteWithin(Timeout);
        sut.IsIdle.Should().BeTrue();
    }

    [TestMethod]
    public void IdleDetector_MultipleTokens_CooldownStartsOnlyAfterAllDisposed()
    {
        // Arrange
        IdleDetector sut = MakeSut(cooldownSeconds: 0);
        TaskCompletionSource becameIdle = new();
        sut.BecameIdle += (_, _) => becameIdle.TrySetResult();

        // Act
        IDisposable token1 = sut.StartNonIdle();
        IDisposable token2 = sut.StartNonIdle();

        token1.Dispose();

        // Assert: BecameIdle should not fire while token2 is still held
        becameIdle.Task.Should().NotBeComplete();
        sut.IsIdle.Should().BeFalse();

        token2.Dispose();

        becameIdle.Task.Should().BeCompleteWithin(Timeout);
        sut.IsIdle.Should().BeTrue();
    }

    [TestMethod]
    public void IdleDetector_StartNonIdleDuringCooldown_ResetsTimer()
    {
        // Arrange: use a long cooldown so we can reliably acquire a token before it fires
        IdleDetector sut = MakeSut(cooldownSeconds: 30);
        int becameIdleCount = 0;
        sut.BecameIdle += (_, _) => Interlocked.Increment(ref becameIdleCount);

        // Act: release token1 to start a 30-second cooldown, then immediately acquire token2
        IDisposable token1 = sut.StartNonIdle();
        token1.Dispose();

        IDisposable token2 = sut.StartNonIdle();  // cancels the cooldown timer

        // Assert: still non-idle and BecameIdle has not fired
        sut.IsIdle.Should().BeFalse();
        becameIdleCount.Should().Be(0, "BecameIdle should not fire while a token is held");

        token2.Dispose();

        // After releasing token2, a new 30-second cooldown starts — BecameIdle has still not fired
        becameIdleCount.Should().Be(0, "BecameIdle should not fire immediately after token2 disposed (cooldown still running)");
    }

    [TestMethod]
    public void IdleDetector_DisposeToken_IdempotentDispose()
    {
        // Arrange: let the initial construction cooldown settle before counting.
        IdleDetector sut = MakeSut(cooldownSeconds: 0);
        TaskCompletionSource initialIdle = new();
        sut.BecameIdle += (_, _) => initialIdle.TrySetResult();
        if (sut.IsIdle)
        {
            initialIdle.TrySetResult();
        }

        initialIdle.Task.Should().BeCompleteWithin(Timeout);

        int becameIdleCount = 0;
        sut.BecameIdle += (_, _) => Interlocked.Increment(ref becameIdleCount);
        TaskCompletionSource becameIdle = new();
        sut.BecameIdle += (_, _) => becameIdle.TrySetResult();
        IDisposable token = sut.StartNonIdle();

        // Act: dispose twice — should not decrement count below zero or double-fire
        token.Dispose();
        token.Dispose();

        // Assert: BecameIdle fires exactly once
        becameIdle.Task.Should().BeCompleteWithin(Timeout);
        becameIdleCount.Should().Be(1);
        sut.IsIdle.Should().BeTrue();
    }
}
