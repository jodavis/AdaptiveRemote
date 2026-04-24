using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Conversation;

internal class ConversationIdleAdapter : MvvmPropertyIdleAdapter
{
    public ConversationIdleAdapter(IRemoteDefinitionService remoteDefinition)
        : base(remoteDefinition.GetElement<ConversationView>(), ConversationView.IsListeningProperty)
    {
    }
}
