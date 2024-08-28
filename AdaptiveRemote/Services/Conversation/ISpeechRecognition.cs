namespace AdaptiveRemote.Services.Conversation;

internal interface ISpeechRecognition
{
    void SetFilter(PhraseKinds filter);

    IAsyncEnumerable<IRecognizedSpeech> RecognizeAsync(CancellationToken stopToken);
}
