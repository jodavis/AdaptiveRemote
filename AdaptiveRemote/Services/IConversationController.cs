namespace AdaptiveRemote.Services;

internal interface IConversationController : IDisposable
{
    void StartListening();
}
