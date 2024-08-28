using System.Speech.Recognition;

namespace AdaptiveRemote.Services.Conversation;

internal interface ISpeechRecognitionEngine
{
    event EventHandler<RecognizedSpeechEventArgs> SpeechRecognized;
    event EventHandler<RecognitionErrorEventArgs> RecognitionError;
    event EventHandler<RecognizedSpeechEventArgs> SpeechRejected;

    void LoadGrammar(Grammar grammar);
    void UnloadGrammar(Grammar grammar);
    void UnloadAllGrammars();
    void RecognizeAsync();
    void RecognizeAsyncCancel();
    void UpdateRecognizerSetting(string name, int value);
}
