using System.Collections.Immutable;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.CloudAssets;

internal class IdleDetector : IIdleDetector
{
    private TaskCompletionSource<ScopedIdleDetector> _scopedIdleDetectorTask = new();

    public async Task WaitForIdleAsync(CancellationToken cancellationToken)
    {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
        ScopedIdleDetector scoped = await _scopedIdleDetectorTask.Task;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks

        await scoped.WaitForIdleAsync(cancellationToken);
    }

    internal class ScopedIdleDetector : IScopedLifecycle
    {
        private readonly TimeSpan _cooldown;
        private readonly IEnumerable<IUserActivityDetector> _userActivityDetectors;
        private readonly IdleDetector _globalIdleDetector;

        public ScopedIdleDetector(IEnumerable<IUserActivityDetector> userActivityDetectors, IIdleDetector globalIdleDetector, IOptions<CloudSettings> settings)
        {
            _cooldown = TimeSpan.FromSeconds(Math.Max(.1, settings.Value.IdleCooldownSeconds));
            _userActivityDetectors = userActivityDetectors.ToImmutableList();
            _globalIdleDetector = globalIdleDetector as IdleDetector
                ?? throw new ArgumentException("Wrong type was injected for IIdleDetector", nameof(globalIdleDetector));
        }

        public string Name => "Idle Detection";

        public Task CleanUpAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
        {
            _globalIdleDetector._scopedIdleDetectorTask = new();
            return Task.CompletedTask;
        }

        public Task InitializeAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
        {
            _globalIdleDetector._scopedIdleDetectorTask.TrySetResult(this);
            return Task.CompletedTask;
        }

        public async Task WaitForIdleAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DateTime mostRecentActivity = _userActivityDetectors
                    .Select(x => x.LastActivityTime)
                    .DefaultIfEmpty(DateTime.MinValue)
                    .Max();

                TimeSpan timeUntilIdle = (mostRecentActivity + _cooldown) - DateTime.Now;

                if (timeUntilIdle <= TimeSpan.Zero)
                {
                    break;
                }

                await Task.Delay(timeUntilIdle, cancellationToken);
            }
        }
    }
}
