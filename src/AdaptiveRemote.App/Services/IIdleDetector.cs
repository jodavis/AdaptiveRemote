namespace AdaptiveRemote.Services;

/// <summary>
/// Tracks whether the user is idle. Non-idle state is held open by calling
/// <see cref="StartNonIdle"/>; the returned <see cref="IDisposable"/> releases the hold when
/// disposed. When all holds are released a cooldown timer starts; <see cref="BecameIdle"/>
/// fires and <see cref="IsIdle"/> becomes <c>true</c> after the cooldown elapses.
/// </summary>
internal interface IIdleDetector
{
    /// <summary>Gets whether the user is currently considered idle.</summary>
    bool IsIdle { get; }

    /// <summary>Raised when the user transitions from non-idle to idle after the cooldown.</summary>
    event EventHandler? BecameIdle;

    /// <summary>
    /// Signals that the user is not idle. The system remains non-idle until every
    /// <see cref="IDisposable"/> returned by this method has been disposed. When the last hold
    /// is released the cooldown timer starts.
    /// </summary>
    IDisposable StartNonIdle();
}
