using System.Speech.Recognition;

namespace AdaptiveRemote.Services.Speech;

internal interface ISpeechRecognitionEngine
{
    event EventHandler<RecognitionResultEventArgs> SpeechRecognized;

    void LoadGrammar(Grammar grammar);
    void RecognizeAsync();
    void RecognizeAsyncCancel();
    void SetInputToDefaultAudioDevice();
    void UnloadAllGrammars();
}
