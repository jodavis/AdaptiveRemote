using System.Text.Json.Serialization;

namespace AdaptiveRemote.Contracts;

// Source-generated JSON context — required for Native AOT Lambda functions;
// shared by all consumers to ensure consistent serialization behaviour.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RawLayout))]
[JsonSerializable(typeof(CompiledLayout))]
[JsonSerializable(typeof(PreviewLayout))]
[JsonSerializable(typeof(ValidationResult))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(IReadOnlyList<RawLayout>))]
[JsonSerializable(typeof(IReadOnlyList<CompiledLayout>))]
public partial class LayoutContractsJsonContext : JsonSerializerContext { }
