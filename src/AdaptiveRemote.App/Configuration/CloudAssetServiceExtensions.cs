using AdaptiveRemote.Services.CloudAssets;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace AdaptiveRemote.Configuration;

internal static class CloudAssetServiceExtensions
{
    internal static IServiceCollection AddCloudAssetServices(this IServiceCollection services)
        => services
            .AddSingleton<ICloudAssetStore, CloudAssetStore>()
            .AddSingleton<CloudAssetOrchestrator>()
            .AddSingleton<IPreScopeInitializer>(sp => sp.GetRequiredService<CloudAssetOrchestrator>())
            .AddHostedService(sp => sp.GetRequiredService<CloudAssetOrchestrator>());

    internal static IServiceCollection AddScopedCloudAsset<T>(
        this IServiceCollection services, string name)
        where T : class
        => services.AddScoped(sp => sp.GetRequiredService<ICloudAssetStore>().Get<T>(name));
}
