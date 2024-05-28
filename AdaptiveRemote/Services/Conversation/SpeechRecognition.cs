using System.IO;
using System.Runtime.CompilerServices;
using System.Speech.Recognition;
using System.Threading.Channels;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Conversation;

internal class SpeechRecognition : ISpeechRecognition, IDisposable
{
    private static readonly string[] SemanticKeyList = ["command", "repeat", "system"];

    private readonly ISpeechRecognitionEngine _engine;
    private readonly ILogger _logger;
    private readonly ConversationSettings _settings;

    private readonly Grammar _attentionGrammar;
    private readonly Grammar _yesNoGrammar;
    private readonly Grammar _commandsGrammar;

    private TaskCompletionSource _attentionTcs = new();
    private TaskCompletionSource<bool> _yesNoTcs = new();
    private Channel<IRecognitionResult> _commandChannel = Channel.CreateUnbounded<IRecognitionResult>();

    private readonly CancellationTokenSource _stopCts = new();
    private readonly object _lockObject = new();
    private int _listeningCount = 0;

    public SpeechRecognition(IOptionsSnapshot<ConversationSettings> settings, ISpeechRecognitionEngine engine, IGrammarProvider grammarProvider, ILogger<SpeechRecognition> logger)
    {
        _engine = engine;
        _logger = logger;
        _settings = settings.Value;

        _attentionGrammar = LoadGrammar(grammarProvider.LoadAttentionGrammar);
        _commandsGrammar = LoadGrammar(grammarProvider.LoadCommandsGrammar);
        _yesNoGrammar = LoadGrammar(grammarProvider.LoadYesNoGrammar);

        _engine.SpeechRecognized += OnSpeechRecognized;
        _engine.RecognitionError += OnRecognitionError;

        _attentionTcs.SetResult();
        _yesNoTcs.SetResult(false);
        _commandChannel.Writer.Complete();

        _settings = settings.Value;
    }

    private void OnRecognitionError(object? sender, RecognitionErrorEventArgs e)
    {
        RecognitionErrorException exception = new RecognitionErrorException(e.Message);
        _logger.LogError(Message.SpeechRecognition_RecognitionError, e.Message);

        _attentionTcs.TrySetException(exception);
        _yesNoTcs.TrySetException(exception);
        _commandChannel.Writer.TryComplete(exception);
    }

    private void OnSpeechRecognized(object? sender, RecognitionResultEventArgs e)
    {
        if (e.Result.TryGetSemanticValue("system", out string? systemCommand))
        {
            switch (systemCommand)
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
            }
        }

        if (e.Result.ContainsSemanticValue("command"))
        {
            _commandChannel.Writer.TryWrite(e.Result);
        }

        if (_settings.RecordSamples)
        {
            RecordSpeechSample(e.Result);
        }
    }

    private Task ResetAttentionTask(CancellationToken cancellationToken)
    {
        lock (_lockObject)
        {
            EnsureCanReset(_attentionTcs.Task, nameof(ISpeechRecognition.ListenForAttentionAsync));

            _attentionTcs = new();
            cancellationToken.Register(() => _attentionTcs.TrySetCanceled());
            return _attentionTcs.Task;
        }
    }

    async Task ISpeechRecognition.ListenForAttentionAsync(CancellationToken cancellationToken)
    {
        cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopCts.Token).Token;

        Task attentionTask = ResetAttentionTask(cancellationToken);

        await StartAndStopListeningAsync(_attentionGrammar, attentionTask);
    }

    private Task<bool> ResetYesNoTask(CancellationToken cancellationToken)
    {
        lock (_lockObject)
        {
            EnsureCanReset(_yesNoTcs.Task, nameof(ISpeechRecognition.ListenForYesNoAsync));

            _yesNoTcs = new();
            cancellationToken.Register(() => _yesNoTcs.TrySetCanceled());
            return _yesNoTcs.Task;
        }
    }

    async Task<bool> ISpeechRecognition.ListenForYesNoAsync(CancellationToken cancellationToken)
    {
        cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopCts.Token).Token;

        Task<bool> yesNoTask = ResetYesNoTask(cancellationToken);

        await StartAndStopListeningAsync(_yesNoGrammar, yesNoTask);

        return yesNoTask.Result;
    }

    private Channel<IRecognitionResult> ResetCommandChannel(CancellationToken cancellationToken)
    {
        lock (_lockObject)
        {
            EnsureCanReset(_commandChannel.Reader.Completion, nameof(ISpeechRecognition.ListenForCommandsAsync));
            _commandChannel = Channel.CreateBounded<IRecognitionResult>(new BoundedChannelOptions(_settings.CommandBufferSize)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest,
                AllowSynchronousContinuations = true,
            });
            cancellationToken.Register(() => _commandChannel.Writer.TryComplete(new TaskCanceledException()));
        }

        return _commandChannel;
    }

    IAsyncEnumerable<IRecognitionResult> ISpeechRecognition.ListenForCommandsAsync(CancellationToken cancellationToken)
    {
        cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopCts.Token).Token;

        Channel<IRecognitionResult> resultChannel = ResetCommandChannel(cancellationToken);

        _ = StartAndStopListeningToChannelAsync(resultChannel);

        return resultChannel.Reader.ReadAllAsync(cancellationToken);

        async Task StartAndStopListeningToChannelAsync(Channel<IRecognitionResult> resultChannel)
        {
            try
            {
                await StartAndStopListeningAsync(_commandsGrammar, resultChannel.Reader.Completion, nameof(ISpeechRecognition.ListenForCommandsAsync));
            }
            catch (Exception ex)
            {
                resultChannel.Writer.TryComplete(ex);
            }
        }
    }

    private async Task StartAndStopListeningAsync(Grammar grammar, Task listenTask, [CallerMemberName] string methodName = "Undefined")
    {
        try
        {
            EnableGrammar(grammar, true);
            StartListening();

            await listenTask;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(Message.SpeechRecognition_CancelledListeningMethod, methodName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(Message.SpeechRecognition_ErrorInListeningMethod, methodName, ex);
            throw;
        }
        finally
        {
            StopListening();
            EnableGrammar(grammar, false);
        }
    }

    private void EnsureCanReset(Task task, string methodName)
    {
        if (!task.IsCompleted)
        {
            _logger.LogAndThrowError(Message.SpeechRecognition_ListeningMethodAlreadyInProgress, methodName);
        }
    }

    private Grammar LoadGrammar(Func<Grammar> loader)
    {
        string grammarName = "Name not loaded";
        try
        {
            Grammar grammar = loader();
            grammarName = grammar.Name;
            grammar.Enabled = false;
            _engine.LoadGrammar(grammar);
            return grammar;
        }
        catch (Exception ex)
        {
            _logger.LogError(Message.SpeechRecognition_GrammarFailedToLoad, grammarName, ex);
            throw;
        }
    }

    private void UnloadGrammar(Grammar grammar)
    {
        try
        {
            grammar.Enabled = false;
            _engine.UnloadGrammar(grammar);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(Message.SpeechRecognition_GrammarFailedToUnload, grammar.Name, ex);
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
            _logger.LogInformation(Message.SpeechRecognition_GrammarEnabled, grammar.Name, grammar.Enabled);
        }
    }

    private void StartListening()
    {
        if (_listeningCount++ == 0)
        {
            _engine.RecognizeAsync();
            _logger.LogInformation(Message.SpeechRecognition_Listening, true);
        }
    }

    private void StopListening()
    {
        if (--_listeningCount == 0)
        {
            try
            {
                _engine.RecognizeAsyncCancel();
                _logger.LogInformation(Message.SpeechRecognition_Listening, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(Message.SpeechRecognition_ErrorInStopListening, ex);
            }
        }
    }

    public void Dispose()
    {
        _stopCts.Cancel();

        UnloadGrammar(_attentionGrammar);
        UnloadGrammar(_yesNoGrammar);
        UnloadGrammar(_commandsGrammar);
    }

    private void RecordSpeechSample(IRecognitionResult result)
    {
        string outputPath = _settings.RecordingOutputPath ?? ".";
        string userName = _settings.RecordingUserName ?? "User";

        string text = result.Text.ToPascalCase();
        string fileName = $"{userName}_{text}_{DateTime.Now:hhmmss}";
        string path = $"{outputPath}\\{fileName}.wav";
        using (Stream waveStream = File.OpenWrite(path))
        {
            result.WriteToWaveStream(waveStream);
            waveStream.Flush();
        }

        string dataRowsPath = $"{outputPath}\\Datarows.txt";
        string semantics = string.Join(",", SemanticKeyList
            .Select(key => result.TryGetSemanticValue(key, out string? value) ? $"{key}={value}" : null)
            .OfType<string>());

        using (Stream dataRowsStream = File.Open(dataRowsPath, FileMode.Append))
        {
            StreamWriter writer = new(dataRowsStream);
            writer.WriteLine($"    [DataRow(\"{fileName}\", \"{result.Text}\", \"{semantics}\", DisplayName = \"{fileName}\")]");
            writer.Flush();
        }
    }
}
