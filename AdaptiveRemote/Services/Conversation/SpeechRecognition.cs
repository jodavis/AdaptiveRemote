using System.Speech.Recognition;
using System.Threading.Channels;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Conversation;

internal class SpeechRecognition : ISpeechRecognition
{
    private readonly ConversationSettings _settings;
    private readonly ISpeechRecognitionEngine _engine;
    private readonly IListeningController _listeningController;
    private readonly ILogger<SpeechRecognition> _logger;

    private readonly Grammar _wakeWordGrammar;
    private readonly Grammar _commandsGrammar;
    private readonly Grammar _confirmationGrammar;

    public SpeechRecognition(IOptions<ConversationSettings> settings, ISpeechRecognitionEngine engine, IListeningController listeningController, IGrammarProvider grammarProvider, ILogger<SpeechRecognition> logger)
    {
        _settings = settings.Value;
        _engine = engine;
        _listeningController = listeningController;
        _logger = logger;

        _engine.UnloadAllGrammars();

        LoadGrammarIntoEngine(_wakeWordGrammar = grammarProvider.LoadAttentionGrammar());
        LoadGrammarIntoEngine(_commandsGrammar = grammarProvider.LoadCommandsGrammar());
        LoadGrammarIntoEngine(_confirmationGrammar = grammarProvider.LoadYesNoGrammar());

        void LoadGrammarIntoEngine(Grammar grammar)
        {
            grammar.Enabled = false;
            _engine.LoadGrammar(grammar);
        }
    }

    void ISpeechRecognition.SetFilter(PhraseKinds filter)
    {
        _wakeWordGrammar.Enabled = filter.HasFlag(PhraseKinds.WakeWord);
        _commandsGrammar.Enabled = filter.HasFlag(PhraseKinds.Commands);
        _confirmationGrammar.Enabled = filter.HasFlag(PhraseKinds.Confirmation);

        if (filter != PhraseKinds.None)
        {
            bool isWakeWord = filter == PhraseKinds.WakeWord;

            _engine.UpdateRecognizerSetting(RecognizerConstants.CPUUsage,
                isWakeWord ? _settings.WakeWordCPU : _settings.ListeningCPU);
            _engine.UpdateRecognizerSetting(RecognizerConstants.ConfidenceThreshold,
                isWakeWord ? _settings.WakeWordConfidenceThreshold : _settings.ListeningConfidenceThreshold);
        }
    }

    IAsyncEnumerable<IRecognizedSpeech> ISpeechRecognition.RecognizeAsync(CancellationToken stopToken)
    {
        Channel<IRecognizedSpeech> channel = Channel.CreateBounded<IRecognizedSpeech>(_settings.CommandBufferSize);

        _ = StartlisteningAsync();

        return channel.Reader.ReadAllAsync(stopToken);

        async Task StartlisteningAsync()
        {
            try
            {
                if (!stopToken.IsCancellationRequested)
                {
                    EventHandler<RecognizedSpeechEventArgs> handler = (sender, args) => channel.Writer.TryWrite(args.Result);
                    using (_listeningController.Listen())
                    {
                        _engine.SpeechRecognized += handler;

                        await stopToken.WaitForCancelled();

                        _engine.SpeechRecognized -= handler;
                    }
                }
                channel.Writer.TryComplete();
            }
            catch (Exception error)
            {
                channel.Writer.TryComplete(error);
            }
        }
    }
}
