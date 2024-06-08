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

    private void UpdateListenState(int listenDelta = 0, int pauseDelta = 0, bool undoOnError = true)
    {
        lock (_lockObject)
        {
            Message errorMessage = Message.ListeningController_RecognizeAsyncError;

            _listenCount += listenDelta;
            _pauseCount += pauseDelta;

            try
            {
                if (_isListening)
                {
                    if (_listenCount == 0 || _pauseCount > 0)
                    {
                        errorMessage = Message.ListeningController_RecognizeAsyncCancelError;

                        _engine.RecognizeAsyncCancel();
                        _isListening = false;
                    }
                }
                else
                {
                    if (_listenCount > 0 && _pauseCount == 0)
                    {
                        errorMessage = Message.ListeningController_RecognizeAsyncError;

                        _engine.RecognizeAsync();
                        _isListening = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(errorMessage, ex);

                if (undoOnError)
                {
                    _listenCount -= listenDelta;
                    _pauseCount -= pauseDelta;

                    throw;
                }
            }
            finally
            {
                _logger.LogInformation(Message.ListeningController_State, _isListening, _listenCount, _pauseCount);
            }
        }
    }

    private class Listener : IDisposable
    {
        private ListeningController? _owner;

        public Listener(ListeningController owner)
        {
            _owner = owner;
            _owner.UpdateListenState(listenDelta: 1);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.UpdateListenState(listenDelta: -1, undoOnError: false);
        }
    }

    private class Pauser : IDisposable
    {
        private ListeningController? _owner;

        public Pauser(ListeningController owner)
        {
            _owner = owner;
            _owner.UpdateListenState(pauseDelta: 1);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.UpdateListenState(pauseDelta: -1, undoOnError: false);
        }
    }
}
