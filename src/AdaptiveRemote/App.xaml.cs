using System.Windows;
using AdaptiveRemote.Models;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            WindowsAcceleratedServices accelerator = CreateAcceleratedServices(e.Args);

            accelerator.MainWindow.Show();
            accelerator.ViewModel.ShutdownCommand = new ActionCommand(Shutdown);

            base.OnStartup(e);

            _ = RunApplicationLoopAndShutdownAsync(accelerator);
        }
        catch (Exception startupFailure)
        {
            MessageBox.Show(
                startupFailure.ToString(),
                "WPF window failure",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            throw;
        }
    }

    protected virtual WindowsAcceleratedServices CreateAcceleratedServices(string[] args) => new(args);

    private async Task RunApplicationLoopAndShutdownAsync(WindowsAcceleratedServices accelerator)
    {
        await accelerator.RunApplicationLoopAsync();

        // If the application loop completes without error, it means
        // the app is shutting down normally. If it ends with an exception
        // there is an error which should be displayed in the UI, so the
        // UI is responsible for shutting down.
        Shutdown();
    }
}

