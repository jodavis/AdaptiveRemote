using System.Speech.Recognition;

namespace AdaptiveRemote.Services.Conversation;

internal interface ISpeechRecognitionEngine
{
    event EventHandler<RecognitionResultEventArgs> SpeechRecognized;
    event EventHandler<RecognitionErrorEventArgs> RecognitionError;

    void LoadGrammar(Grammar grammar);
    void UnloadGrammar(Grammar grammar);
    void RecognizeAsync();
    void RecognizeAsyncCancel();
    void SetInputToDefaultAudioDevice();
}
