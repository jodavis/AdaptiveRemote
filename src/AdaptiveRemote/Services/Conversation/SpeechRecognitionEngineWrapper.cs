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
    private readonly WpfHostMessageLogger _logger;

    private event EventHandler<RecognizedSpeechEventArgs>? _speechRecognized;
    private event EventHandler<RecognizedSpeechEventArgs>? _speechRejected;

    public SpeechRecognitionEngineWrapper(ILogger<SpeechRecognitionEngine> logger, IAudioConfigurationService audioConfiguration)
    {
        _logger = new(logger);

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
        => _logger.SpeechRecognitionEngine_Recognized(new(e.Result));
    private void OnSpeechRecognitionRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
        => _logger.SpeechRecognitionEngine_RecognitionRejected(new(e.Result));
    private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
        => _logger.SpeechRecognitionEngine_Hypothesized(new(e.Result));
    private void OnSpeechDetected(object? sender, SpeechDetectedEventArgs e)
        => _logger.SpeechRecognitionEngine_Detected(e.AudioPosition);
    private void OnRecognizerUpdateReached(object? sender, RecognizerUpdateReachedEventArgs e)
        => _logger.SpeechRecognitionEngine_UpdateReached(e.AudioPosition, e.UserToken);
    private void OnAudioStateChanged(object? sender, AudioStateChangedEventArgs e)
        => _logger.SpeechRecognitionEngine_AudioStateChanged(e.AudioState);
    private void OnAudioSignalProblemOccurred(object? sender, AudioSignalProblemOccurredEventArgs e)
        => _logger.SpeechRecognitionEngine_AudioSignalProblemOccurred(e.AudioSignalProblem, e.RecognizerAudioPosition, e.AudioPosition, e.AudioLevel);

    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        if (e.Error is null)
        {
            _logger.SpeechRecognitionEngine_RecognizeCompleted(e.InputStreamEnded, e.Cancelled, e.BabbleTimeout, e.InitialSilenceTimeout, new(e.Result));
        }
        else
        {
            _logger.SpeechRecognitionEngine_RecognizeCompletedWithError(e.InputStreamEnded, e.Cancelled, e.BabbleTimeout, e.InitialSilenceTimeout, new(e.Result), e.Error);
        }
    }

    private void OnLoadGrammarCompleted(object? sender, LoadGrammarCompletedEventArgs e)
    {
        if (e.Error is null)
        {
            _logger.SpeechRecognitionEngine_LoadGrammarCompleted(e.Grammar.Name, e.Cancelled);
        }
        else
        {
            _logger.SpeechRecognitionEngine_LoadGrammarCompletedWithError(e.Grammar.Name, e.Cancelled, e.Error);
        }
    }

    public void Dispose() => _engine.Dispose();

    internal readonly struct ResultWrapper : IRecognizedSpeech
    {
        private readonly RecognitionResult? _result;

        internal ResultWrapper(RecognitionResult? result)
        {
            _result = result;
        }

        string IRecognizedSpeech.Text => _result?.Text ?? "(null)";
        int IRecognizedSpeech.Confidence => (int)((_result?.Confidence ?? 0) * 100);

        bool IRecognizedSpeech.ContainsSemanticValue(string key) => _result?.Semantics.ContainsKey(key) ?? false;
        void IRecognizedSpeech.WriteToWaveStream(Stream waveStream) => _result?.Audio.WriteToWaveStream(waveStream);

        bool IRecognizedSpeech.TryGetSemanticValue(string key, [NotNullWhen(true)] out string? value)
        {
            if (_result is not null &&
                _result.Semantics.ContainsKey(key) &&
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
            => _result is null
                ? "(null)"
                : $"""
                  Text: {_result.Text}
                  Words: [{string.Join("/", _result.Words.Select(x => $"{x.Pronunciation} {x.Confidence}"))}]
                  Confidence: {_result.Confidence}
                  Alternates: '{string.Join("', '", _result.Alternates.Select(x => $"{x.Text}:{x.Confidence}"))}'
                  Homophones: '{string.Join("', '", _result.Homophones.Select(x => $"{x.Text}:{x.Confidence}"))}'
                  Semantics: {string.Join(", ", _result.Semantics.Select(x => $"{x.Key}:{x.Value.Value}"))}
                  Grammar: {_result.Grammar?.Name ?? "(null)"}
                  """;
    }
}
