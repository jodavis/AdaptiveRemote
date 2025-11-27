using AdaptiveRemote.Services;
using AdaptiveRemote.Services.SystemWrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Configuration;

internal static class SystemWrapperHostBuilderExtensions
{
    internal static IHostBuilder AddSystemWrapperServices(this IHostBuilder builder)
        => builder.ConfigureServices(services => services.AddSystemWrappers());

    internal static IServiceCollection AddSystemWrappers(this IServiceCollection services)
        => services
            .AddSingleton<INetworking, SystemNetWrapper>()
            .AddSingleton<IFileSystem, SystemIOWrapper>();
}
