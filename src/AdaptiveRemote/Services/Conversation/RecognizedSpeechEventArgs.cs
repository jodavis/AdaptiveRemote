namespace AdaptiveRemote.Services.Conversation;

public class RecognizedSpeechEventArgs : EventArgs
{
    internal RecognizedSpeechEventArgs(IRecognizedSpeech result)
    {
        Result = result;
    }

    public IRecognizedSpeech Result { get; }
}
