using System.Threading.Channels;
using AdaptiveRemote.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Conversation;

internal class SpeechRecognition : ISpeechRecognition
{
    private static readonly IEnumerable<PhraseKinds> GrammarKinds =
        [
            PhraseKinds.WakeWord,
            PhraseKinds.Commands,
            PhraseKinds.Confirmation,
            PhraseKinds.Correction
        ];

    private readonly ConversationSettings _settings;
    private readonly ISpeechRecognitionEngine _engine;
    private readonly IListeningController _listeningController;
    private readonly ILogger<SpeechRecognition> _logger;

    private readonly IReadOnlyDictionary<PhraseKinds, IGrammar> _grammars;

    public SpeechRecognition(IOptions<ConversationSettings> settings, ISpeechRecognitionEngine engine, IListeningController listeningController, IGrammarProvider grammarProvider, ILogger<SpeechRecognition> logger)
    {
        _settings = settings.Value;
        _engine = engine;
        _listeningController = listeningController;
        _logger = logger;

        _engine.UnloadAllGrammars();

        _grammars = GrammarKinds.ToDictionary(x => x, x => LoadGrammarIntoEngine(grammarProvider.LoadGrammar(x)));

        IGrammar LoadGrammarIntoEngine(IGrammar grammar)
        {
            grammar.Enabled = false;
            _engine.LoadGrammar(grammar);
            return grammar;
        }
    }

    void ISpeechRecognition.SetFilter(PhraseKinds filter)
    {
        foreach (KeyValuePair<PhraseKinds, IGrammar> grammar in _grammars)
        {
            grammar.Value.Enabled = filter.HasFlag(grammar.Key);
        }

        if (filter != PhraseKinds.None)
        {
            bool isWakeWord = filter == PhraseKinds.WakeWord;

            ConfigureConfidenceThreshold(isWakeWord
                ? _settings.WakeWordConfidenceThreshold
                : _settings.ListeningConfidenceThreshold);
        }
    }

    private void ConfigureConfidenceThreshold(int threshold)
    {
        try
        {
            _engine.SetConfidenceThreshold(threshold);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(Message.SpeechRecognition_CouldNotConfigureSetting, "ConfidenceThreshold", ex.Message);
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
