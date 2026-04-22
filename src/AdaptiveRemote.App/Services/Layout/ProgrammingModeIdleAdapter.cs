using System.ComponentModel;
using AdaptiveRemote.Models;
using AdaptiveRemote.Services.CloudAssets;

namespace AdaptiveRemote.Services.Layout;

internal class ProgrammingModeIdleAdapter : IScopedLifecycle
{
    private readonly LifecycleView _lifecycleView;
    private readonly IIdleDetector _idleDetector;
    private IDisposable? _nonIdleToken;

    public ProgrammingModeIdleAdapter(LifecycleView lifecycleView, IIdleDetector idleDetector)
    {
        _lifecycleView = lifecycleView;
        _idleDetector = idleDetector;
    }

    public string Name => "Programming mode idle adapter";

    public Task InitializeAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        _lifecycleView.PropertyChanged += OnPropertyChanged;
        if (_lifecycleView.IsProgrammingMode)
        {
            _nonIdleToken = _idleDetector.StartNonIdle();
        }
        return Task.CompletedTask;
    }

    public Task CleanUpAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        _lifecycleView.PropertyChanged -= OnPropertyChanged;
        Interlocked.Exchange(ref _nonIdleToken, null)?.Dispose();
        return Task.CompletedTask;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LifecycleView.IsProgrammingMode))
        {
            return;
        }

        if (_lifecycleView.IsProgrammingMode)
        {
            _nonIdleToken ??= _idleDetector.StartNonIdle();
        }
        else
        {
            Interlocked.Exchange(ref _nonIdleToken, null)?.Dispose();
        }
    }
}
