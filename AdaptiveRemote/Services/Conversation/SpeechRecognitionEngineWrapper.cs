using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Speech.Recognition;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Conversation;

[ExcludeFromCodeCoverage(Justification = "Simple wrapper around System.Speech, with event logging")]
internal class SpeechRecognitionEngineWrapper : ISpeechRecognitionEngine, IDisposable
{
    private readonly SpeechRecognitionEngine _engine = new(new CultureInfo("en-US"));
    private readonly ILogger<SpeechRecognitionEngine> _logger;
    private event EventHandler<RecognitionResultEventArgs>? _speechRecognized;

    public SpeechRecognitionEngineWrapper(ILogger<SpeechRecognitionEngine> logger)
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
    }

    private void BroadcastSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        => _speechRecognized?.Invoke(this, new RecognitionResultEventArgs(new ResultWrapper(e.Result)));

    public void UnloadAllGrammars() => _engine.UnloadAllGrammars();
    public void LoadGrammar(Grammar grammar) => _engine.LoadGrammar(grammar);
    public void SetInputToDefaultAudioDevice() => _engine.SetInputToDefaultAudioDevice();
    public void RecognizeAsync() => _engine.RecognizeAsync(RecognizeMode.Multiple);
    public void RecognizeAsyncCancel() => _engine.RecognizeAsyncCancel();

    public event EventHandler<RecognitionResultEventArgs> SpeechRecognized
    {
        add => _speechRecognized += value;
        remove => _speechRecognized -= value;
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        => _logger.LogInformation(LoggingMessages.SpeechRecognitionEngine_Recognized, new ResultWrapper(e.Result));
    private void OnSpeechRecognitionRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
        => _logger.LogWarning(LoggingMessages.SpeechRecognitionEngine_RecognitionRejected, new ResultWrapper(e.Result));
    private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
        => _logger.LogInformation(LoggingMessages.SpeechRecognitionEngine_Hypothesized, new ResultWrapper(e.Result));
    private void OnSpeechDetected(object? sender, SpeechDetectedEventArgs e)
        => _logger.LogInformation(LoggingMessages.SpeechRecognitionEngine_Detected, e.AudioPosition);
    private void OnRecognizerUpdateReached(object? sender, RecognizerUpdateReachedEventArgs e)
        => _logger.LogWarning(LoggingMessages.SpeechRecognitionEngine_UpdateReached, e.AudioPosition, e.UserToken);
    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
        => _logger.LogInformation(LoggingMessages.SpeechRecognitionEngine_RecognizeCompleted,
            e.InputStreamEnded, e.Cancelled, e.BabbleTimeout, e.Error, e.InitialSilenceTimeout, new ResultWrapper(e.Result));
    private void OnLoadGrammarCompleted(object? sender, LoadGrammarCompletedEventArgs e)
        => _logger.LogInformation(LoggingMessages.SpeechRecognitionEngine_LoadGrammarCompleted, e.Error, e.Grammar.Name, e.Cancelled);
    private void OnAudioStateChanged(object? sender, AudioStateChangedEventArgs e)
        => _logger.LogInformation(LoggingMessages.SpeechRecognitionEngine_AudioStateChanged, e.AudioState);
    private void OnAudioSignalProblemOccurred(object? sender, AudioSignalProblemOccurredEventArgs e)
        => _logger.LogWarning(LoggingMessages.SpeechRecognitionEngine_AudioSignalProblemOccurred, e.AudioSignalProblem, e.RecognizerAudioPosition, e.AudioLevel, e.AudioPosition);

    public void Dispose() => _engine.Dispose();

    private class ResultWrapper : IRecognitionResult
    {
        private readonly RecognitionResult _result;

        internal ResultWrapper(RecognitionResult result)
        {
            _result = result;
        }

        string IRecognitionResult.Text => _result.Text;

        string IRecognitionResult.SemanticMeaning => _result.Semantics.Value?.ToString() ?? _result.Text;

        public override string ToString()
            => string.Format(
                string.Join("\n   ",
                    "Text: {0}",
                    "Alternates: '{1}'",
                    "Semantics: {2}"),
                _result.Text,
                string.Join("', '", _result.Alternates),
                string.Join(", ", _result.Semantics.Select(x => $"{x.Key}:{x.Value}")));
    }
}
