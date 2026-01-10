namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Represents a browser debugging endpoint used by the testing subsystem.
/// </summary>
/// <remarks>
/// Implementations expose the TCP port number on which the browser's remote
/// debugging protocol is available. This is typically used by headless or
/// instrumented browser instances created for automated tests.
/// </remarks>
public interface IBrowserDebuggerAccess
{
    /// <summary>
    /// Gets the TCP port number that the browser debugging endpoint is listening on.
    /// </summary>
    /// <value>
    /// An integer representing the TCP port for the browser's remote debugging protocol.
    /// Valid port numbers are in the range 1–65535.
    /// </value>
    int Port { get; }
}
