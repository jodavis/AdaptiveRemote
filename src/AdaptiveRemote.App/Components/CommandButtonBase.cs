using System.ComponentModel;
using Microsoft.AspNetCore.Components;

namespace AdaptiveRemote.Components;

/// <summary>
/// Base class for command button components, providing shared property change subscription,
/// CSS class computation, and disposal logic.
/// Subclasses define which delegate determines the enabled state and what is invoked on click.
/// </summary>
public abstract class CommandButtonBase : ComponentBase, IDisposable
{
    private PropertyChangedEventHandler? _propertyChangedHandler;

    [Parameter]
    public Models.Command? Command { get; set; }

    protected bool IsActive => Command?.IsActive ?? false;
    protected abstract bool IsEnabled { get; }
    protected string ID => Command?.CSSID ?? string.Empty;
    protected string Label => Command?.Label ?? string.Empty;

    protected string CssClasses => string.Join(" ", ComputeCssClasses());

    protected virtual IEnumerable<string> ComputeCssClasses()
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

    protected override void OnInitialized()
    {
        if (Command is not null)
        {
            _propertyChangedHandler = (sender, args) => { _ = InvokeAsync(StateHasChanged); };
            Command.PropertyChanged += _propertyChangedHandler;
        }

        base.OnInitialized();
    }

    public void Dispose()
    {
        if (Command is not null && _propertyChangedHandler is not null)
        {
            Command.PropertyChanged -= _propertyChangedHandler;
        }

        GC.SuppressFinalize(this);
    }

    protected abstract void Invoke();
}
