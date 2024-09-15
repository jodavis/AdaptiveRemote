namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// A system for speaking to the user
/// </summary>
internal interface ISpeechSynthesis
{
    /// <summary>
    /// Speak a given phrase to the user.
    /// </summary>
    /// <returns>A Task that completes when the system is done speaking.</returns>
    /// <remarks>
    /// SayAsync should not be called multiple times simultaneously. Callers should
    /// wait for the Task to complete before starting another phrase.
    /// </remarks>
    Task SayAsync(string phrase, CancellationToken cancellationToken);
}
