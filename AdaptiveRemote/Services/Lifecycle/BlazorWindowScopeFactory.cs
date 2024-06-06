namespace AdaptiveRemote.Services.Lifecycle;

internal class BlazorWindowScopeFactory : IApplicationScopeFactory, IApplicationScope
{
    private readonly MainWindow _mainWindow;

    public BlazorWindowScopeFactory(MainWindow mainWindow, IServiceProvider serviceProvider)
    {
        _mainWindow = mainWindow;
        _mainWindow.Browser.Services = serviceProvider;
    }

    Task<IApplicationScope> IApplicationScopeFactory.CreateNewScopeAsync(CancellationToken cancellationToken)
    {
        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }
        else
        {
            _mainWindow.Browser.WebView.Reload();
        }

        return Task.FromResult<IApplicationScope>(this);
    }

    public async Task TryInvokeAsync(Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken cancellationToken)
    {
        TaskCompletionSource tcs = new();
        while (true)
        {
            bool success = await _mainWindow.Browser.TryDispatchAsync(async scopedServices =>
            {
                try
                {
                    await workItem.Invoke(scopedServices, cancellationToken);
                    tcs.TrySetResult();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            if (success)
            {
                break;
            }

            await Task.Delay(150, cancellationToken);
        }

        await tcs.Task;
    }

    public void Dispose() { }
}
