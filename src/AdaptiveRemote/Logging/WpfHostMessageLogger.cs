using AdaptiveRemote.Services.Conversation;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Logging;

internal partial class WpfHostMessageLogger
{
    private readonly ILogger _logger;

    public WpfHostMessageLogger(ILogger logger)
    {
        _logger = logger;
    }

    [LoggerMessage(EventId = 101, Level = LogLevel.Information, Message = "Recognized {SpeechResult}")]
    public partial void SpeechRecognitionEngine_Recognized(SpeechRecognitionEngineWrapper.ResultWrapper speechResult);

    [LoggerMessage(EventId = 102, Level = LogLevel.Warning, Message = "Recognition rejected: {SpeechResult}")]
    public partial void SpeechRecognitionEngine_RecognitionRejected(SpeechRecognitionEngineWrapper.ResultWrapper speechResult);

    [LoggerMessage(EventId = 103, Level = LogLevel.Information, Message = "Hypothesized: {SpeechResult}")]
    public partial void SpeechRecognitionEngine_Hypothesized(SpeechRecognitionEngineWrapper.ResultWrapper speechResult);

    [LoggerMessage(EventId = 104, Level = LogLevel.Information, Message = "Detected: {AudioPosition}")]
    public partial void SpeechRecognitionEngine_Detected(TimeSpan audioPosition);

    [LoggerMessage(EventId = 105, Level = LogLevel.Warning, Message = "Recognizer update reached at {AudioPosition} ({UserToken})")]
    public partial void SpeechRecognitionEngine_UpdateReached(TimeSpan audioPosition, object userToken);

    [LoggerMessage(EventId = 106, Level = LogLevel.Information, Message = "Recognize completed: InputStreamEnded:{InputStreamEnded} Cancelled:{Cancelled} BabbleTimeout:{BabbleTimeout} InitialSilenceTimeout:{InitialSilenceTimeout}\n   Result:{SpeechResult}")]
    public partial void SpeechRecognitionEngine_RecognizeCompleted(bool inputStreamEnded, bool cancelled, bool babbleTimeout, bool initialSilenceTimeout, SpeechRecognitionEngineWrapper.ResultWrapper speechResult);

    [LoggerMessage(EventId = 106, Level = LogLevel.Error, Message = "Recognize completed: InputStreamEnded:{InputStreamEnded} Cancelled:{Cancelled} BabbleTimeout:{BabbleTimeout} InitialSilenceTimeout:{InitialSilenceTimeout}\n   Result:{SpeechResult}")]
    public partial void SpeechRecognitionEngine_RecognizeCompletedWithError(bool inputStreamEnded, bool cancelled, bool babbleTimeout, bool initialSilenceTimeout, SpeechRecognitionEngineWrapper.ResultWrapper speechResult, Exception error);

    [LoggerMessage(EventId = 107, Level = LogLevel.Information, Message = "Loaded Grammar {Name}\n   Cancelled:{IsCancelled}")]
    public partial void SpeechRecognitionEngine_LoadGrammarCompleted(string name, bool isCancelled);

    [LoggerMessage(EventId = 107, Level = LogLevel.Error, Message = "Loaded Grammar {Name}\n   Cancelled:{IsCancelled}")]
    public partial void SpeechRecognitionEngine_LoadGrammarCompletedWithError(string name, bool isCancelled, Exception error);

    [LoggerMessage(EventId = 108, Level = LogLevel.Information, Message = "AudioState:{AudioState}")]
    public partial void SpeechRecognitionEngine_AudioStateChanged(System.Speech.Recognition.AudioState audioState);

    [LoggerMessage(EventId = 109, Level = LogLevel.Warning, Message = "AudioSignalProblemOccurred:{AudioSignalProblem} at {RecognizerAudioPosition} ({AudioPosition}) with AudioLevel:{AudioLevel}")]
    public partial void SpeechRecognitionEngine_AudioSignalProblemOccurred(object? audioSignalProblem, TimeSpan recognizerAudioPosition, TimeSpan audioPosition, int audioLevel);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Bookmark {Bookmark} reached in '{Prompt}' at {AudioPosition}")]
    public partial void SpeechSynthesis_BookmarkReached(string bookmark, System.Speech.Synthesis.Prompt prompt, TimeSpan audioPosition);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Speaking completed (Cancelled: {Cancelled})")]
    public partial void SpeechSynthesis_SpeakCompleted(bool cancelled);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Error, Message = "Speaking completed (Cancelled: {Cancelled})")]
    public partial void SpeechSynthesis_SpeakCompletedWithError(bool cancelled, Exception error);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "Speaking progress: '{Text}' at {AudioPosition}")]
    public partial void SpeechSynthesis_SpeakProgress(string text, TimeSpan audioPosition);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Information, Message = "Speaking started")]
    public partial void SpeechSynthesis_SpeakStarted();

    [LoggerMessage(EventId = 2005, Level = LogLevel.Information, Message = "State changed to {State} from {PreviousState}")]
    public partial void SpeechSynthesis_StateChanged(System.Speech.Synthesis.SynthesizerState state, System.Speech.Synthesis.SynthesizerState previousState);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Information, Message = "Voice changed to '{VoiceName}'")]
    public partial void SpeechSynthesis_VoiceChanged(string voiceName);
}
