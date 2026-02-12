using AdaptiveRemote.Services.Testing;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Synchronous wrapper extensions for ITestSpeechRecognitionService to simplify test code.
/// </summary>
public static class ITestSpeechRecognitionServiceExtensions
{
    public const int DefaultTimeoutInSeconds = 60;
    public const int DefaultConfidence = 80;

    /// <summary>
    /// Simulates speaking a phrase (synchronous wrapper).
    /// </summary>
    /// <param name="service">The test speech service.</param>
    /// <param name="text">The text that was "spoken".</param>
    /// <param name="confidence">Confidence level (0-100), defaults to 80.</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation.</param>
    public static void SpeakPhrase(this ITestSpeechRecognitionService service, string text, int confidence = DefaultConfidence, int timeoutInSeconds = DefaultTimeoutInSeconds)
        => service.SpeakPhrase(text, confidence, TimeSpan.FromSeconds(timeoutInSeconds));

    /// <summary>
    /// Simulates speaking a phrase (synchronous wrapper).
    /// </summary>
    /// <param name="service">The test speech service.</param>
    /// <param name="text">The text that was "spoken".</param>
    /// <param name="confidence">Confidence level (0-100).</param>
    /// <param name="timeout">Timeout for the operation.</param>
    public static void SpeakPhrase(this ITestSpeechRecognitionService service, string text, int confidence, TimeSpan timeout)
    {
        bool succeeded = WaitHelpers.WaitForAsyncTask(ct => service.SpeakPhraseAsync(text, confidence, ct), timeout);
        if (!succeeded)
        {
            throw new TimeoutException($"Speaking phrase '{text}' did not complete within timeout.");
        }
    }
}
