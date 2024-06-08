namespace AdaptiveRemote.Services.Conversation;

internal interface ISpeechSynthesis
{
    Task SayAsync(string phrase, CancellationToken cancellationToken);
}
