using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Speech.Recognition;
using AdaptiveRemote.Logging;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Conversation;

[ExcludeFromCodeCoverage(Justification = "Simple wrapper around System.Speech, with event logging")]
internal class SpeechRecognitionEngineWrapper : ISpeechRecognitionEngine, IDisposable
{
    private readonly SpeechRecognitionEngine _engine = new(new CultureInfo("en-US"));
    private readonly ILogger<SpeechRecognitionEngine> _logger;

    private event EventHandler<RecognizedSpeechEventArgs>? _speechRecognized;
    private event EventHandler<RecognizedSpeechEventArgs>? _speechRejected;

    public SpeechRecognitionEngineWrapper(ILogger<SpeechRecognitionEngine> logger, IAudioConfigurationService audioConfiguration)
    {
        _logger = logger;

        _engine.AudioSignalProblemOccurred += OnAudioSignalProblemOccurred;
        _engine.AudioStateChanged += OnAudioStateChanged;
        _engine.LoadGrammarCompleted += OnLoadGrammarCompleted;
        _engine.RecognizeCompleted += OnRecognizeCompleted;
        _engine.RecognizerUpdateReached += OnRecognizerUpdateReached;
        _engine.SpeechDetected += OnSpeechDetected;
        _engine.SpeechHypothesized += OnSpeechHypothesized;
        _engine.SpeechRecognitionRejected += OnSpeechRecognitionRejected;
        _engine.SpeechRecognized += OnSpeechRecognized;

        _engine.SpeechRecognized += BroadcastSpeechRecognized;
        _engine.SpeechRecognitionRejected += BroadcastSpeechRejected;

        audioConfiguration.Configure(_engine);

        foreach (RecognizerInfo recognizer in SpeechRecognitionEngine.InstalledRecognizers())
        {
            LogRecognizerInfo(recognizer, _engine.RecognizerInfo.Id == recognizer.Id);
        }
    }

    private void BroadcastSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        => _speechRecognized?.Invoke(this, new RecognizedSpeechEventArgs(WrapRequired(e.Result)));
    private void BroadcastSpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
        => _speechRejected?.Invoke(this, new RecognizedSpeechEventArgs(WrapRequired(e.Result)));

    public void LoadGrammar(IGrammar grammar)
    {
        ArgumentNullException.ThrowIfNull(grammar, nameof(grammar));

        object? native = grammar.GetNativeGrammar();
        if (native is Grammar g)
        {
            _engine.LoadGrammar(g);
        }
        else
        {
            throw new InvalidOperationException("Cannot load grammar: native Grammar instance unavailable on this platform");
        }
    }

    public void UnloadGrammar(IGrammar grammar)
    {
        ArgumentNullException.ThrowIfNull(grammar, nameof(grammar));

        if (grammar.GetNativeGrammar() is Grammar native)
        {
            _engine.UnloadGrammar(native);
        }
        else
        {
            // nothing to do for non-native grammar
        }
    }

    public void UnloadAllGrammars() => _engine.UnloadAllGrammars();
    public void SetInputToDefaultAudioDevice() => _engine.SetInputToDefaultAudioDevice();
    public void Recognize() => _engine.RecognizeAsync(RecognizeMode.Multiple);
    public void RecognizeAsyncCancel() => _engine.RecognizeAsyncCancel();
    public void SetConfidenceThreshold(int threshold) => _engine.UpdateRecognizerSetting("CFGConfidenceRejectionThreshold", threshold);

    public event EventHandler<RecognizedSpeechEventArgs> SpeechRecognized
    {
        add => _speechRecognized += value;
        remove => _speechRecognized -= value;
    }

    public event EventHandler<RecognizedSpeechEventArgs> SpeechRejected
    {
        add => _speechRejected += value;
        remove => _speechRejected -= value;
    }

    private static ResultWrapper? Wrap(RecognitionResult? result)
       => result is not null ? new ResultWrapper(result) : null;
    private static ResultWrapper WrapRequired(RecognitionResult? result)
       => Wrap(result) ?? throw new ArgumentNullException(nameof(result));

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        => _logger.LogInformation(Message.SpeechRecognitionEngine_Recognized, Wrap(e.Result));
    private void OnSpeechRecognitionRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
        => _logger.LogWarning(Message.SpeechRecognitionEngine_RecognitionRejected, Wrap(e.Result));
    private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
        => _logger.LogInformation(Message.SpeechRecognitionEngine_Hypothesized, Wrap(e.Result));
    private void OnSpeechDetected(object? sender, SpeechDetectedEventArgs e)
        => _logger.LogInformation(Message.SpeechRecognitionEngine_Detected, e.AudioPosition);
    private void OnRecognizerUpdateReached(object? sender, RecognizerUpdateReachedEventArgs e)
        => _logger.LogWarning(Message.SpeechRecognitionEngine_UpdateReached, e.AudioPosition, e.UserToken);
    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
        => _logger.LogInformation(Message.SpeechRecognitionEngine_RecognizeCompleted,
            e.InputStreamEnded, e.Cancelled, e.BabbleTimeout, e.Error, e.InitialSilenceTimeout, Wrap(e.Result));
    private void OnLoadGrammarCompleted(object? sender, LoadGrammarCompletedEventArgs e)
        => _logger.LogInformation(Message.SpeechRecognitionEngine_LoadGrammarCompleted, e.Error, e.Grammar.Name, e.Cancelled);
    private void OnAudioStateChanged(object? sender, AudioStateChangedEventArgs e)
        => _logger.LogInformation(Message.SpeechRecognitionEngine_AudioStateChanged, e.AudioState);
    private void OnAudioSignalProblemOccurred(object? sender, AudioSignalProblemOccurredEventArgs e)
        => _logger.LogWarning(Message.SpeechRecognitionEngine_AudioSignalProblemOccurred, e.AudioSignalProblem, e.RecognizerAudioPosition, e.AudioLevel, e.AudioPosition);

    public void Dispose() => _engine.Dispose();

    private void LogRecognizerInfo(RecognizerInfo recognizerInfo, bool selected)
         => _logger.LogInformation(Message.SpeechRecognitionEngine_RecognizerInfo,
                 recognizerInfo.Name,
                 recognizerInfo.Description,
                 selected,
                 recognizerInfo.Id,
                 recognizerInfo.Culture,
                 string.Concat(recognizerInfo.SupportedAudioFormats.Select(
                     x => string.Format(LoggingMessages.SpeechRecognitionEngine_RecognizerInfo_AudioFormatFormat, x.EncodingFormat, x.SamplesPerSecond * x.BitsPerSample / 1000))),
                 string.Concat(recognizerInfo.AdditionalInfo.Select(
                     x => string.Format(LoggingMessages.SpeechRecognitionEngine_RecognizerInfo_AdditionalInfoFormat, x.Key, x.Value))));

    private class ResultWrapper : IRecognizedSpeech
    {
        private readonly RecognitionResult _result;

        internal ResultWrapper(RecognitionResult result)
        {
            _result = result;
        }

        string IRecognizedSpeech.Text => _result.Text;
        int IRecognizedSpeech.Confidence => (int)(_result.Confidence * 100);

        bool IRecognizedSpeech.ContainsSemanticValue(string key) => _result.Semantics.ContainsKey(key);
        void IRecognizedSpeech.WriteToWaveStream(Stream waveStream) => _result.Audio.WriteToWaveStream(waveStream);

        bool IRecognizedSpeech.TryGetSemanticValue(string key, [NotNullWhen(true)] out string? value)
        {
            if (_result.Semantics.ContainsKey(key) &&
                _result.Semantics[key]?.Value is string v)
            {
                value = v;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public override string ToString()
            => string.Format(
                string.Join("\n   ",
                    "Text: {0}",
                    "Words: [{6}]",
                    "Confidence: {4}",
                    "Alternates: '{1}'",
                    "Homophones: '{7}'",
                    "Semantics: {2}",
                    "Grammar: {5}"),
                _result.Text,
                string.Join("', '", _result.Alternates.Select(x => $"{x.Text}:{x.Confidence}")),
                string.Join(", ", _result.Semantics.Select(x => $"{x.Key}:{x.Value.Value}")),
                _result.Semantics.Value,
                _result.Confidence,
                _result.Grammar?.Name ?? "(null)",
                string.Join("/", _result.Words.Select(x => $"{x.Pronunciation} {x.Confidence}")),
                string.Join("', '", _result.Homophones.Select(x => $"{x.Text}:{x.Confidence}")));
    }
}
