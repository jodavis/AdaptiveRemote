using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Lifecycle;

public class WpfAcceleratedServices : AcceleratedServices
{
    private IBrowserDebuggerAccess? _browserDebugger = null;

    public WpfAcceleratedServices(string[] args)
        : base(args)
    {
        MainWindow = new(ViewModel);

        TestingSettings? settings = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build()
            .GetSection("test")
            .Get<TestingSettings>();

        if (settings?.WebViewRemoteDebuggingPort is int debuggerPort)
        {
            _browserDebugger = new BlazorWebViewDebugger(MainWindow.Browser, debuggerPort);
        }
    }

    public MainWindow MainWindow { get; }

    protected override void AddPrecreatedServices(IServiceCollection services) 
    {
        base.AddPrecreatedServices(services);

        services.AddSingleton(MainWindow);

        if (_browserDebugger is not null)
        {
            services.AddSingleton(_browserDebugger);
        }
    }
}
