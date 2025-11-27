namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// Thin wrapper around System.Speech.Recognition.SpeechRecognitionEngine.
/// </summary>
public interface ISpeechRecognitionEngine
{
    /// <summary>
    /// Raised when the SpeechRecognitionEngine receives input that matches any of its
    /// loaded and enabled Grammar objects.
    /// </summary>
    event EventHandler<RecognizedSpeechEventArgs> SpeechRecognized;

    /// <summary>
    /// Raised when the SpeechRecognitionEngine receives input that does not match any 
    /// of its loaded and enabled Grammar objects.
    /// </summary>
    event EventHandler<RecognizedSpeechEventArgs> SpeechRejected;

    /// <summary>
    /// Synchronously loads a Grammar object.
    /// </summary>
    void LoadGrammar(IGrammar grammar);

    /// <summary>
    /// Unloads a specified Grammar object from the SpeechRecognitionEngine instance.
    /// </summary>
    void UnloadGrammar(IGrammar grammar);

    /// <summary>
    /// Unloads all Grammar objects from the recognizer.
    /// </summary>
    void UnloadAllGrammars();

    /// <summary>
    /// Performs one or more asynchronous speech recognition operations.
    /// </summary>
    void RecognizeAsync();

    /// <summary>
    /// Terminates asynchronous recognition without waiting for the current recognition
    /// operation to complete.
    /// </summary>
    void RecognizeAsyncCancel();

    /// <summary>
    /// Updates the CFGConfidenceRejectionThreshold setting of the recognizer.
    /// </summary>
    /// <param name="threshold"></param>
    void SetConfidenceThreshold(int threshold);
}
