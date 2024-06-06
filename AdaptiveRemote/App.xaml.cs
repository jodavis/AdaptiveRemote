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
using AdaptiveRemote.Services.Configuration;
using Microsoft.Extensions.Configuration;
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
        _ = StartApplicationLoopAsync(e.Args);

        base.OnStartup(e);
    }

    private async Task StartApplicationLoopAsync(string[] args)
    {
        IHost host =
        Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddCommandLine(args);
            })
            .ConfigureServices(services => services.AddWpfBlazorWebView())
            .ConfigureServices(services => services.AddSingleton<MainWindow>())
            .AddRemoteServices()
            .AddConversationSystem()
            .Build();

        await host.RunAsync();

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}

