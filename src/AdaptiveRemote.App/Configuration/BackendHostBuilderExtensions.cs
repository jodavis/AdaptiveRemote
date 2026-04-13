using AdaptiveRemote.Services.Backend;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Configuration;

internal static class BackendHostBuilderExtensions
{
    public static IHostBuilder AddBackendSupport(this IHostBuilder builder)
        => builder.ConfigureServices((context, services) =>
            services.AddBackendServices(context.Configuration));

    private static IServiceCollection AddBackendServices(
        this IServiceCollection services,
        IConfiguration configuration)
        => services
            .Configure<BackendSettings>(configuration.GetSection(SettingsKeys.Backend))
            .AddSingleton<ICognitoTokenService, CognitoTokenService>();
}
