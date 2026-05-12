namespace AdaptiveRemote.Services.Lifecycle;

internal sealed class ApplicationRecycleSignal : IApplicationRecycleSignal, IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource _cts = new();

    public CancellationToken Token
    {
        get
        {
            lock (_sync)
            {
                return _cts.Token;
            }
        }
    }

    public void RequestRecycle()
    {
        lock (_sync)
        {
            _cts.Cancel();
        }
    }

    public void Reset()
    {
        CancellationTokenSource old;
        lock (_sync)
        {
            old = _cts;
            _cts = new CancellationTokenSource();
        }
        old.Dispose();
    }

    public void Dispose()
    {
        CancellationTokenSource old;
        lock (_sync)
        {
            old = _cts;
            _cts = new CancellationTokenSource();
        }
        old.Dispose();
    }
}
