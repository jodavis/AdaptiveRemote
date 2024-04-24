
using System.Speech.Recognition;

namespace AdaptiveRemote.Services.Speech;

internal interface ISpeechRecognition
{
    Task ListenForAttention(CancellationToken cancellationToken);

    Task<bool> ListenForYesNo(CancellationToken cancellationToken);

    IAsyncEnumerable<IRecognitionResult> ListenForCommands(CancellationToken cancellationToken);
}
