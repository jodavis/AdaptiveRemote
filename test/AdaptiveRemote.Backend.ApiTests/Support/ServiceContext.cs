namespace AdaptiveRemote.Backend.ApiTests.Support;

/// <summary>
/// Reqnroll context-injection container shared across all step definition classes
/// within a single scenario.
///
/// Holds:
/// - <see cref="Fixture"/>: the running service instance
/// - <see cref="LastResponse"/> / <see cref="LastResponseBody"/>: the most recent
///   HTTP response, set by <c>When</c> steps and read by <c>Then</c> steps.
///
/// Reqnroll creates one instance per scenario and disposes it at scenario end.
/// </summary>
public class ServiceContext : IDisposable
{
    public ServiceFixture Fixture { get; } = new();

    public HttpResponseMessage? LastResponse { get; set; }
    public string? LastResponseBody { get; set; }

    public void Dispose()
    {
        LastResponse?.Dispose();
        Fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
