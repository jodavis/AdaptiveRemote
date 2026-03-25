using System.Windows;

namespace AdaptiveRemote;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        WpfAppHostRunner runner = CreateAppHostRunner(e.Args);

        base.OnStartup(e);

        _ = RunApplicationLoopAndShutdownAsync(runner);
    }

    protected virtual WpfAppHostRunner CreateAppHostRunner(string[] args) => new(args)
    {
        Shutdown = Shutdown,
    };

    private async Task RunApplicationLoopAndShutdownAsync(WpfAppHostRunner runner)
    {
        await runner.RunAsync(CancellationToken.None);

        // If the application loop completes without error, it means
        // the app is shutting down normally. If it ends with an exception
        // there is an error which should be displayed in the UI, so the
        // UI is responsible for shutting down.
        Shutdown();
    }
}

