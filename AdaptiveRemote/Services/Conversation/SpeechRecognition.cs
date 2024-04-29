using System.Speech.Recognition;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Conversation;

internal class SpeechRecognition : ISpeechRecognition
{
    private readonly ISpeechRecognitionEngine _engine;
    private readonly ILogger _logger;

    private readonly Grammar _attentionGrammar;
    private readonly Grammar _yesNoGrammar;
    private readonly Grammar _commandsGrammar;

    private TaskCompletionSource _attentionTcs = new();
    private TaskCompletionSource<bool> _yesNoTcs = new();
    private Channel<IRecognitionResult> _commandChannel = Channel.CreateUnbounded<IRecognitionResult>();

    private readonly object _lockObject = new();
    private int _listeningCount = 0;

    public SpeechRecognition(ISpeechRecognitionEngine engine, IGrammarProvider grammarProvider, ILogger<SpeechRecognition> logger)
    {
        _engine = engine;
        _logger = logger;

        _engine.SetInputToDefaultAudioDevice();
        _engine.UnloadAllGrammars();

        _attentionGrammar = grammarProvider.LoadAttentionGrammar();
        _attentionGrammar.Enabled = false;
        _engine.LoadGrammar(_attentionGrammar);

        _commandsGrammar = grammarProvider.LoadCommandsGrammar();
        _commandsGrammar.Enabled = false;
        _engine.LoadGrammar(_commandsGrammar);

        _yesNoGrammar = grammarProvider.LoadYesNoGrammar();
        _yesNoGrammar.Enabled = false;
        _engine.LoadGrammar(_yesNoGrammar);

        _engine.SpeechRecognized += OnSpeechRecognized;

        _attentionTcs.SetResult();
        _yesNoTcs.SetResult(false);
        _commandChannel.Writer.Complete();
    }

    private void OnSpeechRecognized(object? sender, RecognitionResultEventArgs e)
    {
        switch (e.Result.SemanticMeaning)
        {
            case "YES":
                _yesNoTcs.TrySetResult(true);
                break;
            case "NO":
                _yesNoTcs.TrySetResult(false);
                break;
            case "STARTLISTENING":
                _attentionTcs.TrySetResult();
                break;
            case "STOPLISTENING":
                _commandChannel.Writer.TryComplete();
                break;
            default:
                if (e.Result.SemanticMeaning.StartsWith("Command:") == true)
                {
                    _commandChannel.Writer.TryWrite(e.Result);
                }
                break;
        }
    }

    private Task ResetAttentionTask(CancellationToken cancellationToken)
    {
        lock (_lockObject)
        {
            EnsureCanReset(_attentionTcs.Task, nameof(ISpeechRecognition.ListenForAttention));

            _attentionTcs = new();
            cancellationToken.Register(() => _attentionTcs.TrySetCanceled());
            return _attentionTcs.Task;
        }
    }

    async Task ISpeechRecognition.ListenForAttention(CancellationToken cancellationToken)
    {
        Task attentionTask = ResetAttentionTask(cancellationToken);

        try
        {
            EnableGrammar(_attentionGrammar, true);
            StartListening();

            await attentionTask;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(LoggingMessages.SpeechRecognition_CancelledListeningMethod,
                nameof(ISpeechRecognition.ListenForAttention));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, LoggingMessages.SpeechRecognition_ErrorInListeningMethod,
                nameof(ISpeechRecognition.ListenForAttention), ex);
            throw;
        }
        finally
        {
            StopListening();
            EnableGrammar(_attentionGrammar, false);
        }
    }

    private Task<bool> ResetYesNoTask(CancellationToken cancellationToken)
    {
        lock (_lockObject)
        {
            EnsureCanReset(_yesNoTcs.Task, nameof(ISpeechRecognition.ListenForYesNo));

            _yesNoTcs = new();
            cancellationToken.Register(() => _yesNoTcs.TrySetCanceled());
            return _yesNoTcs.Task;
        }
    }

    async Task<bool> ISpeechRecognition.ListenForYesNo(CancellationToken cancellationToken)
    {
        Task<bool> yesNoTask = ResetYesNoTask(cancellationToken);

        try
        {
            EnableGrammar(_yesNoGrammar, true);
            StartListening();

            return await yesNoTask;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(LoggingMessages.SpeechRecognition_CancelledListeningMethod,
                nameof(ISpeechRecognition.ListenForYesNo));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, LoggingMessages.SpeechRecognition_ErrorInListeningMethod,
                nameof(ISpeechRecognition.ListenForYesNo), ex);
            throw;
        }
        finally
        {
            StopListening();
            EnableGrammar(_yesNoGrammar, false);
        }
    }

    private ChannelReader<IRecognitionResult> ResetCommandChannel(CancellationToken cancellationToken)
    {
        ChannelReader<IRecognitionResult> resultReader;
        lock (_lockObject)
        {
            EnsureCanReset(_commandChannel.Reader.Completion, nameof(ISpeechRecognition.ListenForCommands));
            // TODO: Make channel bounds configurable
            _commandChannel = Channel.CreateBounded<IRecognitionResult>(new BoundedChannelOptions(2)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest,
                AllowSynchronousContinuations = true,
            });
            cancellationToken.Register(() => _commandChannel.Writer.TryComplete(new TaskCanceledException()));
            resultReader = _commandChannel.Reader;
        }

        return resultReader;
    }

    IAsyncEnumerable<IRecognitionResult> ISpeechRecognition.ListenForCommands(CancellationToken cancellationToken)
    {
        return Enumerate(ResetCommandChannel(cancellationToken));

        async IAsyncEnumerable<IRecognitionResult> Enumerate(ChannelReader<IRecognitionResult> resultReader)
        {
            try
            {
                EnableGrammar(_commandsGrammar, true);
                StartListening();

                await foreach (IRecognitionResult result in resultReader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    yield return result;
                }
            }
            finally
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation(LoggingMessages.SpeechRecognition_CancelledListeningMethod,
                        nameof(ISpeechRecognition.ListenForCommands));
                }

                StopListening();
                EnableGrammar(_commandsGrammar, false);
            }
        }
    }

    private void EnsureCanReset(Task task, string methodName)
    {
        if (!task.IsCompleted)
        {
            Exception error = new InvalidOperationException(methodName + " is already in progress");
            _logger.LogError(error, error.Message);
            throw error;
        }
    }

    private void EnableGrammar(Grammar grammar, bool enable)
    {
        try
        {
            grammar.Enabled = enable;
        }
        finally
        {
            _logger.LogInformation(LoggingMessages.SpeechRecognition_GrammarEnabled, grammar.Name, grammar.Enabled);
        }
    }

    private void StartListening()
    {
        if (_listeningCount++ == 0)
        {
            _engine.RecognizeAsync();
            _logger.LogInformation(LoggingMessages.SpeechRecognition_Listening, true);
        }
    }

    private void StopListening()
    {
        if (--_listeningCount == 0)
        {
            try
            {
                _engine.RecognizeAsyncCancel();
                _logger.LogInformation(LoggingMessages.SpeechRecognition_Listening, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, LoggingMessages.SpeechRecognition_ErrorInStopListening, ex);
            }
        }
    }
}
