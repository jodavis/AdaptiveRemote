using AdaptiveRemote.Services.SystemWrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Configuration;

internal static class SystemWrapperHostBuilderExtensions
{
    public static IHostBuilder AddSystemWrapperServices(this IHostBuilder builder)
        => builder.ConfigureServices(services => services.AddSystemWrappers());

    public static IServiceCollection AddSystemWrappers(this IServiceCollection services)
        => services
            .AddSingleton<INetworking, SystemNetWrapper>()
            .AddSingleton<IFileSystem, SystemIOWrapper>();
}
