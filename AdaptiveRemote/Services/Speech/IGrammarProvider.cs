using System.Speech.Recognition;

namespace AdaptiveRemote.Services.Speech;

public interface IGrammarProvider
{
    Grammar LoadAttentionGrammar();
    Grammar LoadCommandsGrammar();
    Grammar LoadYesNoGrammar();
}
