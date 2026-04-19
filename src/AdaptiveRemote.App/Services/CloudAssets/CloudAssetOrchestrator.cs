using AdaptiveRemote.Contracts;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.CloudAssets;

internal class CloudAssetOrchestrator : BackgroundService, IPreScopeInitializer
{
    private readonly ICloudAssetStore _store;
    private readonly TaskCompletionSource _initCompleted = new();

    public CloudAssetOrchestrator(ICloudAssetStore store)
    {
        _store = store;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        CompiledLayout layout = new(
            Id: Guid.Empty,
            RawLayoutId: Guid.Empty,
            UserId: "stub",
            IsActive: true,
            Version: 1,
            Elements:
            [
                new LayoutGroupDefinitionDto("DPAD",
                [
                    new CommandDefinitionDto(CommandType.TiVo, "Up",       "Up",       null, "Sent Up",        "Down",      "Up"),
                    new CommandDefinitionDto(CommandType.TiVo, "Down",     "Down",     null, "Sent Down",      "Up",        "Down"),
                    new CommandDefinitionDto(CommandType.TiVo, "Left",     "Left",     null, "Sent Left",      "Right",     "Left"),
                    new CommandDefinitionDto(CommandType.TiVo, "Right",    "Right",    null, "Sent Right",     "Left",      "Right"),
                    new CommandDefinitionDto(CommandType.TiVo, "Select",   "Select",   null, "Sent Select",    null,        "Select"),
                    new CommandDefinitionDto(CommandType.TiVo, "Back",     "Back",     null, "Sent Back",      null,        "Back"),
                    new CommandDefinitionDto(CommandType.IR,   "Power",    "Power",    null, "Sent Power",     "Power",     "Power"),
                    new CommandDefinitionDto(CommandType.IR,   "PowerOn",  "PowerOn",  null, "Sent PowerOn",   "PowerOff",  "PowerOn"),
                    new CommandDefinitionDto(CommandType.IR,   "PowerOff", "PowerOff", null, "Sent PowerOff",  "PowerOn",   "PowerOff"),
                ]),
                new LayoutGroupDefinitionDto("WELL",
                [
                    new CommandDefinitionDto(CommandType.TiVo, "TiVo",    "TiVo",    null, "Sent TiVo",    null, "TiVo"),
                    new CommandDefinitionDto(CommandType.TiVo, "Netflix",  "Netflix",  null, "Sent Netflix",  null, "Netflix"),
                    new CommandDefinitionDto(CommandType.TiVo, "Guide",    "Guide",    null, "Sent Guide",    null, "Guide"),
                ]),
                new LayoutGroupDefinitionDto("PLAYBACK",
                [
                    new CommandDefinitionDto(CommandType.TiVo, "Play",   "Play",   null, "Sent Play",   "Pause",  "Play"),
                    new CommandDefinitionDto(CommandType.TiVo, "Pause",  "Pause",  null, "Sent Pause",  "Play",   "Pause"),
                    new CommandDefinitionDto(CommandType.TiVo, "Record", "Record", null, "Sent Record", null,     "Record"),
                    new CommandDefinitionDto(CommandType.TiVo, "Skip",   "Skip",   null, "Sent Skip",   "Replay", "Skip"),
                    new CommandDefinitionDto(CommandType.TiVo, "Replay", "Replay", null, "Sent Replay", "Skip",   "Replay"),
                ]),
                new LayoutGroupDefinitionDto("CHANNELANDVOLUME",
                [
                    new CommandDefinitionDto(CommandType.TiVo, "ChannelUp",   "Up",   null, "Sent Channel Up",   "ChannelDown", "ChannelUp"),
                    new CommandDefinitionDto(CommandType.TiVo, "ChannelDown", "Down", null, "Sent Channel Down", "ChannelUp",   "ChannelDown"),
                    new CommandDefinitionDto(CommandType.IR,   "VolumeUp",    "Up",   null, "Sent Volume Up",    "VolumeDown",  "VolumeUp"),
                    new CommandDefinitionDto(CommandType.IR,   "VolumeDown",  "Down", null, "Sent Volume Down",  "VolumeUp",    "VolumeDown"),
                    new CommandDefinitionDto(CommandType.IR,   "Mute",        "Mute", null, "Sent Mute",         "Mute",        "Mute"),
                ]),
            ],
            CssDefinitions: "",
            CompiledAt: DateTimeOffset.UtcNow);

        _store.SetLayout(layout);
        _initCompleted.SetResult();

        return Task.CompletedTask;
    }

    public Task WaitAsync(ILifecycleActivity activity, CancellationToken ct)
    {
        activity.Description = "Loading cloud assets";
        return _initCompleted.Task.WaitAsync(ct);
    }
}
