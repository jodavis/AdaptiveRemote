namespace AdaptiveRemote.Services.Conversation;

public interface IRecognitionResult
{
    string Text { get; }
    string SemanticMeaning { get; }
}
