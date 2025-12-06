
using System.Net;

namespace AdaptiveRemote.Services.TiVo;

internal interface ITiVoConnection
{
    /// <summary>
    /// Send an IR command to the TiVo
    /// </summary>
    /// <param name="commandId">
    /// The ID of the IR command
    /// </param>
    /// <param name="cancellationToken">
    /// Indicates whether the service should stop trying to send the command
    /// </param>
    /// <returns>
    /// A Task representing the state of the send
    ///   Incomplete - The command is still being sent
    ///   Complete - The command was sent successfully
    ///   Faulted - There was an error while sending the command
    ///   Cancelled - The send was cancelled cleanly
    /// </returns>
    Task SendIRCommandAsync(string commandId, CancellationToken cancellationToken);

    /// <summary>
    /// Disconnect from the TiVo. This will typically happen if the 
    /// application is shutting down or is resetting due to settings
    /// changes.
    /// </summary>
    /// <param name="cancellationToken">
    /// Indicates whether the cleanup should be aborted.
    /// </param>
    /// <returns>
    /// A Task representing the state of the Dispose
    ///   Incomplete - The connection is still being disconnected
    ///   Complete - The connection was cleanly disconnected
    ///   Faulted - The connect was not cleanly disconnected, and the system
    ///     is in an unknown state
    /// </returns>
    Task DisposeAsync(CancellationToken cancellationToken);

    internal interface Factory
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
}
