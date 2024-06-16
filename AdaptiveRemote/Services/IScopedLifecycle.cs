namespace AdaptiveRemote.Services;

internal interface IScopedLifecycle
{
    /// <summary>
    /// A description of the service, used for logging status messages and
    /// reporting errors to the user
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Start up the service. This should be a short-running process
    /// </summary>
    /// <param name="cancellationToken">
    /// Indicates that initialization should be aborted, for example
    /// if the app is shutting down or there was a setup failure somehwere
    /// else in the system.
    /// </param>
    /// <returns>
    /// A task indicating the status of initialization:
    ///    Incomplete: Initialization is still in progress
    ///    Complete: The service is successfully initialized
    ///    Cancelled: Initialization was aborted and cleanly rolled back
    ///    Faulted: Something went wrong and the system is in an unknown state
    /// </returns>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Clean up the service. This should reset the system so it can be
    /// restarted. The call can wait for long-running tasks to shut down
    /// cleanly.
    /// </summary>
    /// <param name="cancellationToken">
    /// Indicates that graceful shutdown has been aborted, and the application
    /// should shut down hard.
    /// </param>
    /// <returns>
    /// A task indicating the status of cleanup:
    ///    Incomplete: Clean-up is still in progress, or waiting for long-running tasks to end
    ///    Complete: Clean-up is successful and the system is ready to restart or shut down
    ///    Cancelled: Clean-up was aborted, and any long-running tasks have been cleanly aborted
    ///    Faulted: Something went wrong and the system is in an unknown state.
    /// </returns>
    Task CleanUpAsync(CancellationToken cancellationToken);
}
