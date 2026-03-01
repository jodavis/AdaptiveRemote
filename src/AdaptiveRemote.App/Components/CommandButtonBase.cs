using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Components;

/// <summary>
/// Base class for command button components, providing shared property change subscription,
/// CSS class computation, and disposal logic.
/// Subclasses define which delegate determines the enabled state and what is invoked on click.
/// </summary>
public abstract class CommandButtonBase : MvvmComponent<Models.Command>, IDisposable
{
    protected bool IsActive => ViewModel?.IsActive ?? false;
    protected abstract bool IsEnabled { get; }
    protected string ID => ViewModel?.CSSID ?? string.Empty;
    protected string Label => ViewModel?.Label ?? string.Empty;

    protected string CssClasses => string.Join(" ", ComputeCssClasses());

    protected IEnumerable<string> ComputeCssClasses()
    {
        yield return "btn-primary";

        if (IsActive)
        {
            yield return "btn-active";
        }

        if (!IsEnabled)
        {
            yield return "btn-disabled";
        }
    }

    protected abstract void Invoke();
}
