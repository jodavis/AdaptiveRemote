using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Models;

public class LifecycleView : MvvmObject
{
    public static readonly MvvmProperty<string> TaskNameProperty = new(nameof(TaskName));
    public static readonly MvvmProperty<Exception?> FatalErrorProperty = new(nameof(FatalError));
    public static readonly MvvmProperty<LifecyclePhase> CurrentPhaseProperty = new(nameof(CurrentPhase));

    public string TaskName
    {
        get => GetValue(TaskNameProperty);
        set => SetValue(TaskNameProperty, value);
    }

    public Exception? FatalError
    {
        get => GetValue(FatalErrorProperty);
        set => SetValue(FatalErrorProperty, value);
    }

    public LifecyclePhase CurrentPhase
    {
        get => GetValue(CurrentPhaseProperty);
        set => SetValue(CurrentPhaseProperty, value);
    }
}
