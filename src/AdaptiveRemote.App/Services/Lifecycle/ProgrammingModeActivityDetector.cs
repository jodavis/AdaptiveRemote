using AdaptiveRemote.Models;
using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Services.Lifecycle;

internal class ProgrammingModeActivityDetector : MvvmPropertyActivityDetector
{
    public ProgrammingModeActivityDetector(LifecycleView lifecycleView)
        : base(lifecycleView, LifecycleView.IsProgrammingModeProperty)
    {
    }
}
