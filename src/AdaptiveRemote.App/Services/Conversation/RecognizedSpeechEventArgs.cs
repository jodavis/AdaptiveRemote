namespace AdaptiveRemote.Services.Conversation;

public class RecognizedSpeechEventArgs : EventArgs
{
    public RecognizedSpeechEventArgs(IRecognizedSpeech result)
    {
        Result = result;
    }

    public IRecognizedSpeech Result { get; }
}
