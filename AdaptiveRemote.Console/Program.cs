using System.Windows;
using AdaptiveRemote.Services.Lifecycle;

namespace AdaptiveRemote;

internal class Program : Application
{
    private readonly AcceleratedServices _accelerator;

    public Program(string[] args)
    {
        _accelerator = new(args);
    }

    [STAThread]
    private static void Main(string[] args)
    {
        new Program(args).Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _ = StartApplicationLoopAsync();
    }

    private async Task StartApplicationLoopAsync()
    {
        try
        {
            await _accelerator.StartApplicationLoopAsync();
        }
        finally
        {
            Dispatcher.Invoke(Shutdown);
        }
    }
}
