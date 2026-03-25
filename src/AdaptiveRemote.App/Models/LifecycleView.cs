using System.Windows.Input;
using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Models;

public class LifecycleView : MvvmObject
{
    public static readonly MvvmProperty<string> TaskNameProperty = new(nameof(TaskName));
    public static readonly MvvmProperty<string?> FatalErrorProperty = new(nameof(FatalError));
    public static readonly MvvmProperty<string?> FatalErrorDetailProperty = new(nameof(FatalErrorDetail));
    public static readonly MvvmProperty<LifecyclePhase> CurrentPhaseProperty = new(nameof(CurrentPhase));
    public static readonly MvvmProperty<bool> ShowErrorProperty = new(nameof(ShowError));
    public static readonly MvvmProperty<bool> ShowBrowserProperty = new(nameof(ShowBrowser), defaultValue: true);
    public static readonly MvvmProperty<bool> ShowButtonPanelProperty = new(nameof(ShowButtonPanel));
    public static readonly MvvmProperty<ICommand> ShutdownCommandProperty = new(nameof(ShutdownCommand));
    public static readonly MvvmProperty<ICommand> ShowErrorCommandProperty = new(nameof(ShowErrorCommand));
    public static readonly MvvmProperty<ICommand> CloseErrorCommandProperty = new(nameof(CloseErrorCommand));
    public static readonly MvvmProperty<bool> IsProgrammingModeProperty = new(nameof(IsProgrammingMode));

    public string TaskName
    {
        get => GetValue(TaskNameProperty);
        set => SetValue(TaskNameProperty, value);
    }

    public string? FatalError
    {
        get => GetValue(FatalErrorProperty);
        set => SetValue(FatalErrorProperty, value);
    }

    public string? FatalErrorDetail
    {
        get => GetValue(FatalErrorDetailProperty);
        set => SetValue(FatalErrorDetailProperty, value);
    }

    public LifecyclePhase CurrentPhase
    {
        get => GetValue(CurrentPhaseProperty);
        set => SetValue(CurrentPhaseProperty, value);
    }

    public bool ShowError
    {
        get => GetValue(ShowErrorProperty);
        set => SetValue(ShowErrorProperty, value);
    }

    public bool ShowBrowser
    {
        get => GetValue(ShowBrowserProperty);
        set => SetValue(ShowBrowserProperty, value);
    }

    public bool ShowButtonPanel
    {
        get => GetValue(ShowButtonPanelProperty);
        set => SetValue(ShowButtonPanelProperty, value);
    }

    public ICommand ShutdownCommand
    {
        get => GetValue(ShutdownCommandProperty);
        set => SetValue(ShutdownCommandProperty, value);
    }

    public ICommand ShowErrorCommand
    {
        get => GetValue(ShowErrorCommandProperty);
        set => SetValue(ShowErrorCommandProperty, value);
    }

    public ICommand CloseErrorCommand
    {
        get => GetValue(CloseErrorCommandProperty);
        set => SetValue(CloseErrorCommandProperty, value);
    }

    public bool IsProgrammingMode
    {
        get => GetValue(IsProgrammingModeProperty);
        set => SetValue(IsProgrammingModeProperty, value);
    }

    /// <summary>
    /// A <see cref="CancellationToken"/> that is cancelled when learning mode is exited.
    /// Set by <see cref="AdaptiveRemote.Services.ILifecycleViewController.EnterLearningMode"/>
    /// and remains cancelled when not in learning mode.
    /// </summary>
    internal CancellationToken LearningCancellationToken { get; set; } = new CancellationToken(canceled: true);
}
