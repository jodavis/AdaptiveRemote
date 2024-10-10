using AdaptiveRemote.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Lifecycle;

public class AcceleratedServices
{
    private IHostBuilder _hostBuilder;

    public LifecycleView ViewModel { get; }
    internal ILifecycleViewController Controller { get; }
    internal DiagnosticAdapter DiagnosticAdapter { get; }
    public MainWindow MainWindow { get; }

    public AcceleratedServices(string[] args)
    {
        ViewModel = new();
        Controller = new LifecycleViewController(ViewModel);
        DiagnosticAdapter = new(Controller);

        MainWindow = new(ViewModel);

        Controller.SetPhase(LifecyclePhase.Waiting);

        _hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureAppSettings(args)
            .ConfigureApp()
            .ConfigureServices(AddPrecreatedServices);
    }

    private void AddPrecreatedServices(IServiceCollection services)
        => services
            .AddSingleton(MainWindow)
            .AddSingleton(Controller)
            .AddSingleton(ViewModel);

    public async Task StartApplicationLoopAsync()
    {
        MainWindow.Show();

        await Task.Run(async () =>
        {
            try
            {
                IHost host = _hostBuilder.Build();
                await host.RunAsync();
            }
            catch (Exception configErrors)
            {
                Controller.SetFatalError(configErrors);
            }
            finally
            {
                Controller.SetPhase(LifecyclePhase.CleaningUp);
            }
        });
    }
}
