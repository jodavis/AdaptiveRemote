using System.Windows;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote;

public class WpfAppHostRunner : AppHostRunner
{
    public required Action Shutdown { get; init; }

    public WpfAppHostRunner(string[] commandLineArgs)
        : base(commandLineArgs)
    { }

    protected override AcceleratedServices CreateAcceleratedServices()
    {
        WpfAcceleratedServices accelerator = new(CommandLineArguments);

        try
        {
            accelerator.ViewModel.ShutdownCommand = new ActionCommand(Shutdown);
            accelerator.MainWindow.Show();
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

        return accelerator;
    }

    protected override IHostBuilder ConfigureHostBuilder()
        => base.ConfigureHostBuilder()
            .AddWindowsUIServices()
            .AddWindowsSpeechServices();
}
