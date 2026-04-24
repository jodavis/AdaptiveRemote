using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Lifecycle;

internal class ProgrammingModeIdleAdapter : MvvmPropertyIdleAdapter
{
    public ProgrammingModeIdleAdapter(LifecycleView lifecycleView)
        : base(lifecycleView, LifecycleView.IsProgrammingModeProperty)
    {
    }
}
