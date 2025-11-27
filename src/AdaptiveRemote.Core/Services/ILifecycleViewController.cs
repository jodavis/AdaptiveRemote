namespace AdaptiveRemote.Services;

internal interface ILifecycleViewController
{
    /// <summary>
    /// Report an unrecoverable error that should stop the normal operation of
    /// the application.
    /// </summary>
    void SetFatalError(Exception error);

    /// <summary>
    /// Sets the lifecycle phase, typically an initialization phase that determines
    /// the default status message, or Ready to switch the application into normal
    /// operation.
    /// </summary>
    void SetPhase(LifecyclePhase phase);

    /// <summary>
    /// Creates a lifecycle task that can be used to report status. The task is disposed
    /// when it is complete.
    /// </summary>
    ILifecycleActivity StartTask(string description);
}
