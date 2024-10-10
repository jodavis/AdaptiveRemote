using System.Windows;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote;

public partial class App : Application
{
    private string[]? _args;

    public App()
    {
        _args = null;
    }

    public App(string[] args)
    {
        _args = args;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _ = StartApplicationLoopAsync(_args ?? e.Args);

        base.OnStartup(e);
    }

    private async Task StartApplicationLoopAsync(string[] args)
    {
        AcceleratedServices accelerator = new(args);

        await accelerator.StartApplicationLoopAsync();

        // If the application loop completes without error, it means
        // the app is shutting down normally. If it ends with an exception
        // there is an error which should be displayed in the UI, so the
        // UI is responsible for shutting down.
        Shutdown();
    }
}

