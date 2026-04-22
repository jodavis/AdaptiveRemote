using System.ComponentModel;
using AdaptiveRemote.Models;
using AdaptiveRemote.Services.CloudAssets;

namespace AdaptiveRemote.Services.Layout;

internal class ConversationIdleAdapter : IScopedLifecycle
{
    private readonly IRemoteDefinitionService _remoteDefinition;
    private readonly IIdleDetector _idleDetector;
    private ConversationView? _view;
    private IDisposable? _nonIdleToken;

    public ConversationIdleAdapter(IRemoteDefinitionService remoteDefinition, IIdleDetector idleDetector)
    {
        _remoteDefinition = remoteDefinition;
        _idleDetector = idleDetector;
    }

    public string Name => "Conversation idle adapter";

    public Task InitializeAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        _view = _remoteDefinition.GetElement<ConversationView>();
        _view.PropertyChanged += OnPropertyChanged;
        if (_view.IsListening)
        {
            _nonIdleToken = _idleDetector.StartNonIdle();
        }
        return Task.CompletedTask;
    }

    public Task CleanUpAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        if (_view is not null)
        {
            _view.PropertyChanged -= OnPropertyChanged;
        }
        Interlocked.Exchange(ref _nonIdleToken, null)?.Dispose();
        return Task.CompletedTask;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ConversationView.IsListening))
        {
            return;
        }

        if (((ConversationView)sender!).IsListening)
        {
            _nonIdleToken ??= _idleDetector.StartNonIdle();
        }
        else
        {
            Interlocked.Exchange(ref _nonIdleToken, null)?.Dispose();
        }
    }
}
