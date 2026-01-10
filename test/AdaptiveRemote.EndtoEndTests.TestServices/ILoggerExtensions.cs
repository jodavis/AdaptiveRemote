using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndtoEndTests;

public static class ILoggerExtensions
{
    public static IDisposable LogTimedOperation(this ILogger logger, string operationDescription)
    {
        return new TimedOperation(logger, operationDescription);
    }

    private sealed class TimedOperation : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationDescription;
        private readonly DateTime _startTime;

        public TimedOperation(ILogger logger, string operationDescription)
        {
            _logger = logger;
            _operationDescription = operationDescription;
            _startTime = DateTime.Now;

            _logger.LogInformation("{OperationDescription}...", _operationDescription);
        }

        public void Dispose()
        {
            _logger.LogInformation("{OperationDescription} in {ElapsedMs}ms.", _operationDescription, (DateTime.Now - _startTime).TotalMilliseconds);
        }
    }
}
