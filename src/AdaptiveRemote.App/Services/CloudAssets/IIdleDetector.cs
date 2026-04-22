namespace AdaptiveRemote.Services.CloudAssets;

// Tracks whether the user is idle. Non-idle state is held open by calling StartNonIdle();
// the returned IDisposable releases the hold when disposed. When all holds are released,
// a cooldown timer starts; BecameIdle fires and IsIdle becomes true after the cooldown.
internal interface IIdleDetector
{
    bool IsIdle { get; }
    event EventHandler? BecameIdle;
    IDisposable StartNonIdle();
}
