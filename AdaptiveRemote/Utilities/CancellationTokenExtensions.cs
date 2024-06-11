namespace AdaptiveRemote.Utilities;

public static class CancellationTokenExtensions
{
    public static Task WaitForCancelled(this CancellationToken token)
    {
        TaskCompletionSource tcs = new();
        token.Register(tcs.SetResult);
        return tcs.Task;
    }
}
