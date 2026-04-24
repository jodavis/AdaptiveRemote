namespace AdaptiveRemote.Services.CloudAssets;

/// <summary>
/// Shared settings for all cloud asset services.
/// </summary>
internal class CloudSettings
{
    public int IdleCooldownSeconds { get; set; } = 30;
    public int SseMaxConsecutiveFailures { get; set; } = 10;
    public string CachePath { get; set; } = @"%LocalAppData%\AdaptiveRemote\CloudAssets";
    public string StubFilePath { get; set; } = "dev/layout.json";

    /// <summary>
    /// The name of the cloud asset that <see cref="StubFilePath"/> provides content for.
    /// Used by <see cref="FileSystemCloudAssetWatchService"/> to identify which asset changed.
    /// </summary>
    public string AssetName { get; set; } = "layout";
}
