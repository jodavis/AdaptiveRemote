using AdaptiveRemote.Services.Broadlink;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Configuration;

internal static class BroadlinkHostBuilderExtensions
{
    public static IHostBuilder AddBroadlinkSupport(this IHostBuilder builder)
        => builder.ConfigureServices((context, services) => services.AddBroadlinkServices(context.Configuration.GetSection(SettingsKeys.Broadlink)));

    private static IServiceCollection AddBroadlinkServices(this IServiceCollection services, IConfiguration configuration)
        => configuration.GetValue<bool>(nameof(BroadlinkSettings.Fake))
            ? services.AddNullCommandService<Models.IRCommand>()
            : services
                .AddScopedLifecycleService<BroadlinkCommandService>()
                .AddSingleton<IEncryption.Factory, AesWrapper.Factory>()
                .AddScoped<IDeviceLocator, DeviceLocator>()
                .AddSingleton<IDeviceConnection.Factory, DeviceConnection.Factory>()
                .AddSingleton<IUdpService, UdpService>()
                .AddSingleton<ISocket.Factory, SocketWrapper.Factory>()
                .Configure<BroadlinkSettings>(configuration);
}
