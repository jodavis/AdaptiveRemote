namespace AdaptiveRemote.Services.Conversation;

public class RecognitionErrorEventArgs : EventArgs
{
    internal RecognitionErrorEventArgs(string message, Exception? exception = default)
    {
        Message = message;
        Exception = exception;
    }

    public string Message { get; }
    public Exception? Exception { get; }
}
