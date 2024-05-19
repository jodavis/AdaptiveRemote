using System.Windows.Media.Animation;
using System.Xml;
using AdaptiveRemote.Logging;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Conversation;

internal class ListeningController : IListeningController
{
    private readonly ISpeechRecognitionEngine _engine;
    private readonly ILogger<ListeningController> _logger;
    private readonly object _lockObject = new();

    private int _listenCount = 0;
    private int _pauseCount = 0;
    private bool _isListening = false;

    public ListeningController(ISpeechRecognitionEngine engine, ILogger<ListeningController> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    IDisposable IListeningController.Listen() => new Listener(this);

    IDisposable IListeningController.Pause() => new Pauser(this);

    private void IncrementListening()
    {
        lock (_lockObject)
        {
            _listenCount++;
            UpdateListenState();
        }
    }

    private void DecrementListening()
    {
        lock (_lockObject)
        {
            _listenCount--;
            UpdateListenState();
        }
    }

    private void IncrementPausing()
    {
        lock (_lockObject)
        {
            _pauseCount++;
            UpdateListenState();
        }
    }

    private void DecrementPausing()
    {
        lock (_lockObject)
        {
            _pauseCount--;
            UpdateListenState();
        }
    }

    private void UpdateListenState()
    {
        if (_isListening)
        {
            if (_listenCount == 0 || _pauseCount > 0)
            {
                _isListening = false;
                _engine.RecognizeAsyncCancel();
            }
        }
        else
        {
            if (_listenCount > 0 && _pauseCount == 0)
            {
                _isListening = true;
                _engine.RecognizeAsync();
            }
        }

        _logger.LogInformation(Message.ListeningController_State, _isListening, _listenCount, _pauseCount);
    }

    private class Listener : IDisposable
    {
        private ListeningController? _owner;

        public Listener(ListeningController owner)
        {
            _owner = owner;
            _owner.IncrementListening();
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.DecrementListening();
        }
    }

    private class Pauser : IDisposable
    {
        private ListeningController? _owner;

        public Pauser(ListeningController owner)
        {
            _owner = owner;
            _owner.IncrementPausing();
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.DecrementPausing();
        }
    }
}
