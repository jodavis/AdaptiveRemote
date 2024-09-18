using System.Configuration;
using System.Windows;
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
            IHost host =
            Host.CreateDefaultBuilder()
                .ConfigureAppSettings(args)
                .ConfigureApp()
                .Build();

            await host.RunAsync();
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

