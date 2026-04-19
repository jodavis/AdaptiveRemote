namespace AdaptiveRemote.Services.CloudAssets;

/// <summary>
/// Shared connection and auth settings for all cloud asset services.
/// </summary>
internal class CloudSettings
{
    public string BackendBaseUrl { get; set; } = "";
    public string CognitoTokenEndpointUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public int IdleCooldownSeconds { get; set; } = 30;
    public int SseMaxConsecutiveFailures { get; set; } = 10;
    public string CachePath { get; set; } = @"%LocalAppData%\AdaptiveRemote\CloudAssets";
    public string StubFilePath { get; set; } = "";
}
