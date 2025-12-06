namespace AdaptiveRemote.Services;

public interface ILifecycleActivity : IDisposable
{
    /// <summary>
    /// The initialization status message associated with this activity.
    /// </summary>
    string Description { get; set; }

    /// <summary>
    /// Report an unrecoverable error that should stop the normal operation
    /// of the application.
    /// </summary>
    void SetFatalError(Exception error);
}
