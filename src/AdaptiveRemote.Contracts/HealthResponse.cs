namespace AdaptiveRemote.Contracts;

/// <summary>
/// Standard health check response for all backend services.
/// </summary>
public record HealthResponse(
    string ServiceName,
    string Version,
    string Status
);
