namespace AdaptiveRemote.Services.Lifecycle;

public class LifecycleActivity : ILifecycleActivity
{
    internal LifecycleActivity(string initialDescription)
    {
        Description = initialDescription;
    }

    public virtual string Description { get; set; }

    public virtual void SetFatalError(Exception error) { }

    public virtual void Dispose() => GC.SuppressFinalize(this);
}
