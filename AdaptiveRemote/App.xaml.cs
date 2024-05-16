using System;
using System.Configuration;
using System.Data;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Transactions;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Shell;
using AdaptiveRemote.Services;
using AdaptiveRemote.Services.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Windows.Themes;

namespace AdaptiveRemote;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        _ = StartApplicationLoopAsync();

        base.OnStartup(e);
    }

    private async Task StartApplicationLoopAsync()
    {
        IHost host =
        Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddWpfBlazorWebView())
            .AddRemoteServices()
            .AddConversationServices()
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

