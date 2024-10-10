using System.Configuration;
using System.Windows;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        _ = StartApplicationLoopAsync(e.Args);

        base.OnStartup(e);
    }

    private async Task StartApplicationLoopAsync(string[] args)
    {
        try
        {
            AcceleratedServices accelerator = new(args);

            await accelerator.StartApplicationLoopAsync();

            Shutdown();
        }
        catch (ConfigurationErrorsException configErrors)
        {
            MessageBox.Show(configErrors.Message, "Configuration errors", MessageBoxButton.OK, MessageBoxImage.Stop);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}

