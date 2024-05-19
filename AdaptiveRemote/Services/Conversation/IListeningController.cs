namespace AdaptiveRemote.Services.Conversation;

internal interface IListeningController
{
    IDisposable Listen();

    IDisposable Pause();
}
