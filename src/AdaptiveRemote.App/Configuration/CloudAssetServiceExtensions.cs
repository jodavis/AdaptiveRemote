using AdaptiveRemote.Services;
using AdaptiveRemote.Services.CloudAssets;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdaptiveRemote.Configuration;

internal static class CloudAssetServiceExtensions
{
    internal static IServiceCollection AddCloudAssetServices(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddSingleton<ICloudAssetStore, CloudAssetStore>()
            .AddSingleton<IIdleDetector, IdleDetector>()
            .AddScopedLifecycleService<IdleDetector.ScopedIdleDetector>()
            .AddSingleton<ICloudAssetCache, CloudAssetCache>()
            .AddSingleton<ICloudAssetDownloader, FileCloudAssetDownloader>()
            .AddSingleton<FileSystemCloudAssetWatchService>()
            .AddSingleton<IAssetChangeNotifier>(sp => sp.GetRequiredService<FileSystemCloudAssetWatchService>())
            .AddHostedService(sp => sp.GetRequiredService<FileSystemCloudAssetWatchService>())
            .AddSingleton<CloudAssetOrchestrator>()
            .AddSingleton<IPreScopeInitializer>(sp => sp.GetRequiredService<CloudAssetOrchestrator>())
            .AddHostedService(sp => sp.GetRequiredService<CloudAssetOrchestrator>())
            .Configure<CloudSettings>(configuration);

    internal static IServiceCollection AddScopedCloudAsset<T>(
        this IServiceCollection services, ICloudAsset<T> asset)
        where T : class
        => services
            .AddSingleton<ICloudAsset>(asset)
            .AddScoped(sp => sp.GetRequiredService<ICloudAssetStore>().Get<T>(asset.Name));
}
