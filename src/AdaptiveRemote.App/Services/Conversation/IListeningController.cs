namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// Abstraction that allows multiple components to control whether the
/// system is listening.
/// </summary>
internal interface IListeningController
{
    /// <summary>
    /// Start Listening. If the system is already listening, this adds to the 
    /// reference count of listening requests.
    /// </summary>
    /// <returns>An IDisposable that can be disposed when the system should stop listening.</returns>
    IDisposable Listen();

    /// <summary>
    /// Pause listening. No matter how many listening requests have been made, this will
    /// prevent listening.
    /// </summary>
    /// <returns>An Idisposable that can be disposed when the system can start listening again.</returns>
    /// <remarks>
    /// This is used to pause speech recognition while speech is being synthesized, so
    /// that the application won't listen to its own speech.
    /// </remarks>
    IDisposable Pause();
}
