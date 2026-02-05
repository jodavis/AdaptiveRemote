namespace AdaptiveRemote.Headless.Logging;

internal partial class HeadlessHostMessageLogger
{
    private readonly ILogger _logger;

    public HeadlessHostMessageLogger(ILogger logger)
    {
        _logger = logger;
    }
}
