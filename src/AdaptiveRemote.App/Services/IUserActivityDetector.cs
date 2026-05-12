namespace AdaptiveRemote.Services;

internal interface IUserActivityDetector
{
    DateTime LastActivityTime { get; }
}
