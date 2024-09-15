namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// A system for recognizing speech
/// </summary>
internal interface ISpeechRecognition
{
    /// <summary>
    /// Sets a filter for which kinds of phrases should be accepted by the recognizer.
    /// </summary>
    void SetFilter(PhraseKinds filter);

    /// <summary>
    /// Returns a stream of successfully recognized speech. The enumeration will continue
    /// asynchronously until the stopToken is cancelled, at which point it will return
    /// gracefully.
    /// </summary>
    IAsyncEnumerable<IRecognizedSpeech> RecognizeAsync(CancellationToken stopToken);
}
