namespace AdaptiveRemote.Services.Lifecycle;

internal class ApplicationRecycleSignal : IApplicationRecycleSignal
{
    private CancellationTokenSource _cts = new();

    public CancellationToken Token => _cts.Token;

    public void RequestRecycle() => _cts.Cancel();

    public void Reset()
    {
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }
}
