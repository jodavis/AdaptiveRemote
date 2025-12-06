namespace AdaptiveRemote.Services.Conversation;

public class RecognitionErrorException : Exception
{
    internal RecognitionErrorException(string message)
        : base(message)
    { }
}
