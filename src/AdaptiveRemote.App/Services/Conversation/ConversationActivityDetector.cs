using AdaptiveRemote.Models;
using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Services.Conversation;

internal class ConversationActivityDetector : MvvmPropertyActivityDetector
{
    public ConversationActivityDetector(IRemoteDefinitionService remoteDefinition)
        : base(remoteDefinition.GetElement<ConversationView>(), ConversationView.IsListeningProperty)
    {
    }
}
