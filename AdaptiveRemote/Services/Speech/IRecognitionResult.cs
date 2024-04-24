namespace AdaptiveRemote.Services.Speech;

public interface IRecognitionResult
{
    string Text { get; }
    string SemanticMeaning { get; }
}
