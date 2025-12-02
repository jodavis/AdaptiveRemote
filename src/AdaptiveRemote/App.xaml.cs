using System.Windows;
using AdaptiveRemote.Services.Lifecycle;

namespace AdaptiveRemote;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            AcceleratedServices accelerator = CreateAcceleratedServices(e.Args);
            accelerator.ViewModel.ShutdownCommand = new ActionCommand(Shutdown);

            MainWindow mainWindow = new(accelerator.ViewModel);
            mainWindow.Show();

            accelerator.ConfigureHostServices(services =>
                services
                    .AddWindowsUIServices(mainWindow)
                    .AddWindowsSpeechServices());

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

    protected virtual AcceleratedServices CreateAcceleratedServices(string[] args) => new(args);

    private async Task RunApplicationLoopAndShutdownAsync(AcceleratedServices accelerator)
    {
        await accelerator.RunApplicationLoopAsync();

        // If the application loop completes without error, it means
        // the app is shutting down normally. If it ends with an exception
        // there is an error which should be displayed in the UI, so the
        // UI is responsible for shutting down.
        Shutdown();
    }
}

