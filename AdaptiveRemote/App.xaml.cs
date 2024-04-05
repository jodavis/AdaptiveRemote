using System.Windows;
using AdaptiveRemote.Services.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        _ = StartApplicationLoop();

        base.OnStartup(e);
    }

    private async Task StartApplicationLoop()
    {
        IHost host =
        Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddWpfBlazorWebView())
            .AddRemoteServices()
            .AddTraceLogging()
            .ConfigureServices(services => services.AddSingleton<MainWindow>())
            .Build();

        // TODO: What does the restart cycle look like?
        MainWindow window = host.Services.GetRequiredService<MainWindow>();

        IServiceScope scope = host.Services.CreateScope();
        MainWindow.Resources["services"] = scope.ServiceProvider;

        window.Show();
        await host.RunAsync();
        window.Hide();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}

