namespace AdaptiveRemote.Services.Conversation;

public class RecognitionResultEventArgs : EventArgs
{
    internal RecognitionResultEventArgs(IRecognitionResult result)
    {
        Result = result;
    }

    public IRecognitionResult Result { get; }
}
