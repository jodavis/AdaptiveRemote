using System.ComponentModel;
using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Services;

// Subscribes to a bool MvvmProperty on an MvvmObject and holds a non-idle token via
// IIdleDetector while the property is true. Thread-safe: InitializeAsync, CleanUpAsync,
// and OnPropertyChanged all synchronize on _lock, and _subscribed prevents token leaks
// if a PropertyChanged callback races with CleanUpAsync.
internal abstract class MvvmPropertyIdleAdapter : IUserActivityDetector, IDisposable
{
    private readonly MvvmObject _target;
    private readonly MvvmProperty<bool> _property;
    private DateTime? _lastActivityTime;

    protected MvvmPropertyIdleAdapter(MvvmObject target, MvvmProperty<bool> property)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _property = property ?? throw new ArgumentNullException(nameof(property));

        _target.PropertyChanged += OnPropertyChanged;
        _lastActivityTime = target.GetValue(property) ? null : DateTime.MinValue;
    }

    public DateTime LastActivityTime => _lastActivityTime ?? DateTime.Now;

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != _property.Name)
        {
            return;
        }

        if (_target.GetValue(_property))
        {
            _lastActivityTime = null;
        }
        else
        {
            _lastActivityTime ??= DateTime.Now;
        }
    }

    public void Dispose()
    {
        _target.PropertyChanged -= OnPropertyChanged;
    }
}
