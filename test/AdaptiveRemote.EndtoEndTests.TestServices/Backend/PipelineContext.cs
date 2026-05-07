namespace AdaptiveRemote.EndToEndTests.TestServices.Backend;

/// <summary>
/// Reqnroll context-injection container for pipeline integration tests.
/// Holds the <see cref="PipelineServiceFixture"/> that manages both
/// RawLayoutService and LayoutProcessingService for end-to-end scenarios.
///
/// The fixture is started lazily by the first step that needs it
/// (typically "Given the pipeline is running").
///
/// Reqnroll creates one instance per scenario and disposes it at scenario end.
/// </summary>
public sealed class PipelineContext : IDisposable
{
    public PipelineServiceFixture Fixture { get; } = new();

    public HttpResponseMessage? LastResponse { get; set; }
    public string? LastResponseBody { get; set; }

    public void Dispose()
    {
        LastResponse?.Dispose();
        Fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
