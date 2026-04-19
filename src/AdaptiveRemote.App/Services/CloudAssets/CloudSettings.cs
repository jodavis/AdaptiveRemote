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
}
