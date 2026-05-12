using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdaptiveRemote.Models.CloudAssets;

internal sealed class JsonCloudAsset<T>(
    string name,
    string streamUrl,
    string eventName,
    string resourcePath,
    JsonSerializerContext jsonContext)
    : BasicCloudAsset<T>(name, streamUrl, eventName, resourcePath)
{
    public override async Task<object> DeserializeAsync(Stream stream, CancellationToken ct)
    {
        object? result = await JsonSerializer.DeserializeAsync(stream, typeof(T), jsonContext, ct);
        return result ?? throw new InvalidOperationException(
            $"Deserialized null for asset '{Name}'.");
    }
}
