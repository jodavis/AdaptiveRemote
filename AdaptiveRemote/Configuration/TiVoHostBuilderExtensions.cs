using AdaptiveRemote.Services.TiVo;
using AdaptiveRemote.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Configuration;

internal static class TiVoHostBuilderExtensions
{
    public static IHostBuilder AddTiVoSupport(this IHostBuilder builder)
        => builder.ConfigureServices((context, services) => services.AddTiVoServices(context.Configuration.GetSection(SettingsKeys.TiVo)));

    private static IServiceCollection AddTiVoServices(this IServiceCollection services, IConfiguration configuration)
        => configuration.GetValue<bool>(nameof(TiVoSettings.Fake))
            ? services.AddNullCommandService<Models.TiVoCommand>()
            : services
                .AddScopedLifecycleService<TiVoService>()
                .AddSingleton<ITiVoConnection.Factory, LibraryTiVoConnection.Factory>()
                .AddScoped<ITiVoLocator, ScanningTiVoLocator>()
                .AddDecorator<ITiVoLocator, PreviousTiVoLocator>()
                .Configure<TiVoSettings>(configuration);
}
