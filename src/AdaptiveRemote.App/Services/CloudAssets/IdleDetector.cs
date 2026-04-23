using AdaptiveRemote.Services;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.CloudAssets;

internal class IdleDetector : IIdleDetector
{
    private readonly TimeSpan _cooldown;
    private readonly object _lock = new();
    private int _activeTokenCount;
    private Timer? _cooldownTimer;

    public IdleDetector(IOptions<CloudSettings> settings)
    {
        _cooldown = TimeSpan.FromSeconds(Math.Max(0, settings.Value.IdleCooldownSeconds));
        // Start non-idle; the initial cooldown brings the system to idle for the first time.
        ScheduleCooldown();
    }

    public bool IsIdle { get; private set; }
    public event EventHandler? BecameIdle;

    public IDisposable StartNonIdle()
    {
        lock (_lock)
        {
            IsIdle = false;
            CancelCooldown();
            _activeTokenCount++;
        }
        return new NonIdleToken(this);
    }

    private void ReleaseToken()
    {
        lock (_lock)
        {
            _activeTokenCount--;
            if (_activeTokenCount == 0)
            {
                ScheduleCooldown();
            }
        }
    }

    private void ScheduleCooldown()
    {
        // Called while _lock is held (or from the constructor before any other thread can access).
        _cooldownTimer?.Dispose();
        _cooldownTimer = new Timer(OnCooldownElapsed, null, _cooldown, Timeout.InfiniteTimeSpan);
    }

    private void CancelCooldown()
    {
        // Called while _lock is held.
        _cooldownTimer?.Dispose();
        _cooldownTimer = null;
    }

    private void OnCooldownElapsed(object? state)
    {
        EventHandler? handler;
        lock (_lock)
        {
            if (_activeTokenCount > 0)
            {
                return;
            }
            IsIdle = true;
            CancelCooldown();
            handler = BecameIdle;
        }
        handler?.Invoke(this, EventArgs.Empty);
    }

    private sealed class NonIdleToken : IDisposable
    {
        private readonly IdleDetector _detector;
        private int _disposed;

        internal NonIdleToken(IdleDetector detector) => _detector = detector;

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                _detector.ReleaseToken();
            }
        }
    }
}
