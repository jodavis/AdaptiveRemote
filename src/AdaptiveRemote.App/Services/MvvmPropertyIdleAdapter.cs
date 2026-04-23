using System.ComponentModel;
using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Services;

// Subscribes to a bool MvvmProperty on an MvvmObject and holds a non-idle token via
// IIdleDetector while the property is true. Thread-safe: InitializeAsync, CleanUpAsync,
// and OnPropertyChanged all synchronize on _lock, and _subscribed prevents token leaks
// if a PropertyChanged callback races with CleanUpAsync.
internal abstract class MvvmPropertyIdleAdapter : IScopedLifecycle
{
    private readonly MvvmObject _target;
    private readonly MvvmProperty<bool> _property;
    private readonly IIdleDetector _idleDetector;
    private IDisposable? _nonIdleToken;
    private bool _subscribed;
    private readonly object _lock = new();

    protected MvvmPropertyIdleAdapter(MvvmObject target, MvvmProperty<bool> property, IIdleDetector idleDetector)
    {
        _target = target;
        _property = property;
        _idleDetector = idleDetector;
    }

    public abstract string Name { get; }

    public Task InitializeAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _subscribed = true;
            _target.PropertyChanged += OnPropertyChanged;
            if (_target.GetValue(_property))
            {
                _nonIdleToken = _idleDetector.StartNonIdle();
            }
        }
        return Task.CompletedTask;
    }

    public Task CleanUpAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _subscribed = false;
            _target.PropertyChanged -= OnPropertyChanged;
            Interlocked.Exchange(ref _nonIdleToken, null)?.Dispose();
        }
        return Task.CompletedTask;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != _property.Name)
        {
            return;
        }
        lock (_lock)
        {
            if (!_subscribed)
            {
                return;
            }
            if (_target.GetValue(_property))
            {
                _nonIdleToken ??= _idleDetector.StartNonIdle();
            }
            else
            {
                Interlocked.Exchange(ref _nonIdleToken, null)?.Dispose();
            }
        }
    }
}
