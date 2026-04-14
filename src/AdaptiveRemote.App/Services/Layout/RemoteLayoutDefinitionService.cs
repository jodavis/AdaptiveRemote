using AdaptiveRemote.Models;
using AdaptiveRemote.Services.CloudAssets;

namespace AdaptiveRemote.Services.Layout;

/// <summary>
/// RemoteLayoutDefinitionService v1: reads LayoutGroup directly from
/// ICloudAssetStore.Get&lt;LayoutGroup&gt;("layout") and returns it as RemoteRoot.
/// No DTO mapping in this version.
/// </summary>
internal class RemoteLayoutDefinitionService : IRemoteDefinitionService
{
    private readonly ICloudAssetStore _store;

    public RemoteLayoutDefinitionService(ICloudAssetStore store)
    {
        _store = store;
    }

    public RemoteLayoutElement RemoteRoot
    {
        get
        {
            try
            {
                return _store.Get<LayoutGroup>("layout");
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    "Failed to load layout from CloudAssetStore. " +
                    "Ensure CloudAssetOrchestrator has completed initialization before the first scope is created.",
                    ex);
            }
        }
    }
}
