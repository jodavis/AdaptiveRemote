using System.Speech.Recognition;

namespace AdaptiveRemote.Services.Conversation;

internal interface ISpeechRecognitionEngine
{
    event EventHandler<RecognitionResultEventArgs> SpeechRecognized;
    event EventHandler<RecognitionErrorEventArgs> RecognitionError;
    event EventHandler<RecognitionResultEventArgs> SpeechRejected;

    void LoadGrammar(Grammar grammar);
    void UnloadGrammar(Grammar grammar);
    void RecognizeAsync();
    void RecognizeAsyncCancel();
}
