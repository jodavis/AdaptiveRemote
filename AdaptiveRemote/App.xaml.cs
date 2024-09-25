using System.Configuration;
using System.Windows;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
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
        }
        catch (ConfigurationErrorsException configErrors)
        {
            MessageBox.Show(configErrors.Message, "Configuration errors", MessageBoxButton.OK, MessageBoxImage.Stop);
        }
        finally
        {
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}

