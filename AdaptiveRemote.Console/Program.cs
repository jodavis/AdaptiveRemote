using System.Configuration;
using System.Windows;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote;

internal class Program : Application
{
    private readonly IHost _host;

    public Program(string[] args)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppSettings(args)
            .ConfigureApp()
            .Build();
    }

    [STAThread]
    private static void Main(string[] args)
    {
        new Program(args).Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _ = StartApplicationLoopAsync();
    }

    private async Task StartApplicationLoopAsync()
    {
        try
        {
            await _host.RunAsync();
        }
        catch (ConfigurationErrorsException configErrors)
        {
            Console.WriteLine(configErrors.Message);
        }
        finally
        {
            Shutdown();
        }
    }
}
