namespace AdaptiveRemote.Services.Conversation;

internal interface ISpeechRecognition
{
    Task ListenForAttentionAsync(CancellationToken cancellationToken);

    Task<bool> ListenForYesNoAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<IRecognitionResult> ListenForCommandsAsync(CancellationToken cancellationToken);
}
