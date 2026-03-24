using AdaptiveRemote.Services.Broadlink;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Configuration;

internal static class BroadlinkHostBuilderExtensions
{
    public static IHostBuilder AddBroadlinkSupport(this IHostBuilder builder)
        => builder.ConfigureServices((context, services) => services.AddBroadlinkServices(context.Configuration));

    private static IServiceCollection AddBroadlinkServices(this IServiceCollection services, IConfiguration configuration)
        => configuration.GetSection(SettingsKeys.Broadlink).GetValue<bool>(nameof(BroadlinkSettings.Fake))
            ? services.AddNullCommandService<Models.IRCommand>()
            : services
                .AddScopedLifecycleService<BroadlinkCommandService>()
                .AddSingleton<IEncryption.Factory, AesWrapper.Factory>()
                .AddScoped<IDeviceLocator, DeviceLocator>()
                .AddSingleton<IDeviceConnection.Factory, DeviceConnection.Factory>()
                .AddSingleton<IUdpService, UdpService>()
                .AddSingleton<ISocket.Factory, SocketWrapper.Factory>()
                .Configure<BroadlinkSettings>(configuration.GetSection(SettingsKeys.Broadlink))
                .Configure<IRDataSettings>(configuration.GetSection(SettingsKeys.IRData));
}
