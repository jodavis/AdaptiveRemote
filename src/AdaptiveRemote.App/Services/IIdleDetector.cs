namespace AdaptiveRemote.Services;

/// <summary>
/// </summary>
internal interface IIdleDetector
{
    Task WaitForIdleAsync(CancellationToken cancellationToken);
}
