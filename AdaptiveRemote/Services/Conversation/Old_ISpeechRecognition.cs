namespace AdaptiveRemote.Services.Conversation;

internal interface Old_ISpeechRecognition
{
    Task ListenForAttentionAsync(CancellationToken cancellationToken);

    Task<bool> ListenForYesNoAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<IRecognizedSpeech> ListenForCommandsAsync(CancellationToken cancellationToken);

    void ToggleListening();
}
