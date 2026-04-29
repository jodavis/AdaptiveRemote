namespace AdaptiveRemote.Models.CloudAssets;

internal abstract class BasicCloudAsset<T> : ICloudAsset<T>
{
    public string Name { get; }
    public string StreamUrl { get; }
    public string EventName { get; }
    public string ResourcePath { get; }

    protected BasicCloudAsset(string name, string streamUrl, string eventName, string resourcePath)
    {
        Name = name;
        StreamUrl = streamUrl;
        EventName = eventName;
        ResourcePath = resourcePath;
    }

    public abstract Task<object> DeserializeAsync(Stream stream, CancellationToken ct);
}
