using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Lifecycle;

internal class ProgrammingModeIdleAdapter : MvvmPropertyIdleAdapter
{
    public ProgrammingModeIdleAdapter(LifecycleView lifecycleView, IIdleDetector idleDetector)
        : base(lifecycleView, LifecycleView.IsProgrammingModeProperty, idleDetector)
    {
    }

    public override string Name => "Programming mode idle adapter";
}
