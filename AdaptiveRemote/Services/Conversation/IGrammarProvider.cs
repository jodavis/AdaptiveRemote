using System.Speech.Recognition;

namespace AdaptiveRemote.Services.Conversation;

public interface IGrammarProvider
{
    Grammar LoadAttentionGrammar();
    Grammar LoadCommandsGrammar();
    Grammar LoadYesNoGrammar();
}
