using System.Runtime.CompilerServices;

namespace AdaptiveRemote.Services.Speech;

internal class SpeechRecognition : ISpeechRecognition
{
    Task ISpeechRecognition.ListenForAttention(CancellationToken cancellationToken)
        => Task.Delay(10_000, cancellationToken);
    async IAsyncEnumerable<IRecognitionResult> ISpeechRecognition.ListenForCommands([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Delay(5000, cancellationToken);
        yield return new Result { Text = "Up" };
        await Task.Delay(500, cancellationToken);
        yield return new Result { Text = "Up" };
        await Task.Delay(500, cancellationToken);
        yield return new Result { Text = "Play" };
        await Task.Delay(2000, cancellationToken);
        yield return new Result { Text = "Channel Up" };
        await Task.Delay(1000, cancellationToken);
        yield return new Result { Text = "Page Up" };
        await Task.Delay(1000, cancellationToken);
    }

    private class Result : IRecognitionResult
    {
        public required string Text { get; init; }
    }
}
