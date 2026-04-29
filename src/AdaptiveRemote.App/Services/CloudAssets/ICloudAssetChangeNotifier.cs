namespace AdaptiveRemote.Services.CloudAssets;

internal interface ICloudAssetChangeNotifier
{
    /// <summary>
    /// Waits until a change is detected and returns the name of the asset that changed.
    /// </summary>
    Task<string> WaitForChangeAsync(CancellationToken ct);
}
