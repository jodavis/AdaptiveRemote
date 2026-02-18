using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdaptiveRemote.Services.Lifecycle;

public class WpfAcceleratedServices : AcceleratedServices
{
    private readonly IBrowserDebuggerAccess? _browserDebugger;

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

    public override void AddPrecreatedServices(IServiceCollection services)
    {
        base.AddPrecreatedServices(services);

        services.AddSingleton(MainWindow);

        if (_browserDebugger is not null)
        {
            services.AddSingleton(_browserDebugger);
        }
    }
}
