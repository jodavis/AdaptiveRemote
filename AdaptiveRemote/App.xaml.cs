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

    private static async Task StartApplicationLoop()
    {
        IHost host =
        Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddWpfBlazorWebView())
            .AddRemoteServices()
            .AddTraceLogging()
            .ConfigureServices(services => services.AddSingleton<MainWindow>())
            .Build();

        MainWindow window = host.Services.GetRequiredService<MainWindow>();

        window.Show();
        await host.RunAsync();
        window.Hide();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}

