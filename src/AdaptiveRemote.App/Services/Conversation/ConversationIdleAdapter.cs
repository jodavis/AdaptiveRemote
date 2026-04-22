using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Conversation;

internal class ConversationIdleAdapter : MvvmPropertyIdleAdapter
{
    public ConversationIdleAdapter(IRemoteDefinitionService remoteDefinition, IIdleDetector idleDetector)
        : base(remoteDefinition.GetElement<ConversationView>(), ConversationView.IsListeningProperty)
    {
    }
}
