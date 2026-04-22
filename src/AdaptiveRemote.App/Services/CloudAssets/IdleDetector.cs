using System.Collections.Immutable;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.CloudAssets;

internal class IdleDetector : IIdleDetector
{
    private readonly TimeSpan _cooldown;
    private readonly IEnumerable<IUserActivityDetector> _userActivityDetectors;

    public IdleDetector(IEnumerable<IUserActivityDetector> userActivityDetectors, IOptions<CloudSettings> settings)
    {
        _cooldown = TimeSpan.FromSeconds(Math.Max(.1, settings.Value.IdleCooldownSeconds));
        _userActivityDetectors = userActivityDetectors.ToImmutableList();
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
