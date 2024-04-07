
using System.Speech.Recognition;

namespace AdaptiveRemote.Services.Speech;

internal interface ISpeechRecognition
{
    Task ListenForAttention(CancellationToken cancellationToken);
    IAsyncEnumerable<IRecognitionResult> ListenForCommands(CancellationToken cancellationToken);
}
