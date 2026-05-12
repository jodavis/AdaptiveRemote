using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Backend.Common.Logging;

public static class RequestHandlerScopeExtensions
{
    public static IDisposable StartRequestScope(this ILogger logger, string method, string path, string? userId = null)
    {
        if (userId != null)
        {
            logger.AuthenticatedRequestStarted(method, path, userId);
        }
        else
        {
            logger.UnauthenticatedRequestStarted(method, path);
        }

        return new RequestHandlerScope(logger, method, path);
    }
}

internal class RequestHandlerScope : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _method;
    private readonly string _path;
    private bool _disposed = false;

    public RequestHandlerScope(ILogger logger, string method, string path)
    {
        _logger = logger;
        _method = method;
        _path = path;
    }

    public void Dispose()
    {
        if (!Interlocked.Exchange(ref _disposed, true))
        {
            _logger.RequestHandled(_method, _path);
        }
    }
}
