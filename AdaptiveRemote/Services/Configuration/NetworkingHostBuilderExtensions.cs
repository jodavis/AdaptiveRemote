using AdaptiveRemote.Services.Networking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Configuration;

internal static class NetworkingHostBuilderExtensions
{
    public static IHostBuilder AddNetworkingServices(this IHostBuilder builder)
        => builder.ConfigureServices(services => services.AddNetworking());

    public static IServiceCollection AddNetworking(this IServiceCollection services)
        => services
            .AddSingleton<INetworking, SystemNetWrapper>();
}
