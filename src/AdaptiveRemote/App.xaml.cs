using System.Windows;
using AdaptiveRemote.Services.Lifecycle;
using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Web.WebView2.Core;

namespace AdaptiveRemote;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            WpfAcceleratedServices accelerator = CreateAcceleratedServices(e.Args);
            accelerator.ViewModel.ShutdownCommand = new ActionCommand(Shutdown);
            
            accelerator.MainWindow.Show();

            IHostBuilder hostBuilder = Host.CreateDefaultBuilder(e.Args)
                .AddAcceleratedServices(accelerator)
                .AddWindowsUIServices()
                .AddWindowsSpeechServices();

            base.OnStartup(e);

            _ = RunApplicationLoopAndShutdownAsync(hostBuilder, accelerator.Controller);
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

    protected virtual WpfAcceleratedServices CreateAcceleratedServices(string[] args) => new(args);

    private async Task RunApplicationLoopAndShutdownAsync(IHostBuilder hostBuilder, Services.ILifecycleViewController controller)
    {
        await Task.Run(async () =>
        {
            try
            {
                IHost host = hostBuilder.Build();
                await host.RunAsync();
            }
            catch (Exception configErrors)
            {
                controller.SetFatalError(configErrors);
                throw;
            }
            finally
            {
                controller.SetPhase(LifecyclePhase.CleaningUp);
            }
        });

        // If the application loop completes without error, it means
        // the app is shutting down normally. If it ends with an exception
        // there is an error which should be displayed in the UI, so the
        // UI is responsible for shutting down.
        Shutdown();
    }
}

