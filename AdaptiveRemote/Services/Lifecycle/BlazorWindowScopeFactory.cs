using System.Windows;
using Microsoft.AspNetCore.Components.WebView.Wpf;

namespace AdaptiveRemote.Services.Lifecycle;

internal class BlazorWindowScopeFactory : IApplicationScopeFactory, IApplicationScope
{
    private readonly BlazorWebView _browser;

    public BlazorWindowScopeFactory(MainWindow mainWindow, IServiceProvider serviceProvider)
    {
        _browser = mainWindow.Browser;
        _browser.Services = serviceProvider;
    }

    Task<IApplicationScope> IApplicationScopeFactory.CreateNewScopeAsync(CancellationToken cancellationToken)
    {
        if (!_browser.IsVisible)
        {
            _browser.Visibility = Visibility.Visible;
        }
        else
        {
            _browser.WebView.Reload();
        }

        return Task.FromResult<IApplicationScope>(this);
    }

    public async Task TryInvokeAsync(Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken cancellationToken)
    {
        TaskCompletionSource tcs = new();
        while (true)
        {
            bool success = await _browser.TryDispatchAsync(async scopedServices =>
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
