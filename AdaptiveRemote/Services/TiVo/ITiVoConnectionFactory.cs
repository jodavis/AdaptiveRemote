using System.Net;

namespace AdaptiveRemote.Services.TiVo;

internal interface ITiVoConnectionFactory
{
    /// <summary>
    /// Connect to the TiVo and prepare to start sending commands
    /// </summary>
    /// <param name="endpoint">
    /// An EndPoint that can be used to connect to the TiVo
    /// </param>
    /// <param name="cancellationToken">
    /// Indicates whether initialization has been cancelled and should be
    /// aborted cleanly.
    /// </param>
    /// <returns>
    /// A Task representing the state of the connection
    ///   Incomplete - Still connecting
    ///   Complete - Successfully connected
    ///   Faulted - Could not connect to the TiVo
    ///   Cancelled - The process was cleanly aborted due to cancellation
    /// </returns>
    Task<ITiVoConnection> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken);
}
