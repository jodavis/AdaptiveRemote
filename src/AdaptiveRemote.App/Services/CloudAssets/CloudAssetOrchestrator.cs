using AdaptiveRemote.Contracts;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.CloudAssets;

/// <summary>
/// Stub orchestrator that inlines the same hardcoded commands as StaticCommandGroupProvider,
/// expressed as a CompiledLayout DTO (without GUTTER — appended by RemoteLayoutDefinitionService).
/// Stores the CompiledLayout in CloudAssetStore and immediately signals IPreScopeInitializer
/// complete. No file I/O, no HTTP.
/// </summary>
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
                    new CommandDefinitionDto(CommandType.TiVo, "Up",       "Up",       null, "Up",           "Down",      "Up"),
                    new CommandDefinitionDto(CommandType.TiVo, "Down",     "Down",     null, "Down",         "Up",        "Down"),
                    new CommandDefinitionDto(CommandType.TiVo, "Left",     "Left",     null, "Left",         "Right",     "Left"),
                    new CommandDefinitionDto(CommandType.TiVo, "Right",    "Right",    null, "Right",        "Left",      "Right"),
                    new CommandDefinitionDto(CommandType.TiVo, "Select",   "Select",   null, "Select",       null,        "Select"),
                    new CommandDefinitionDto(CommandType.TiVo, "Back",     "Back",     null, "Back",         null,        "Back"),
                    new CommandDefinitionDto(CommandType.IR,   "Power",    "Power",    null, "Power",        "Power",     "Power"),
                    new CommandDefinitionDto(CommandType.IR,   "PowerOn",  "PowerOn",  null, "PowerOn",      "PowerOff",  "PowerOn"),
                    new CommandDefinitionDto(CommandType.IR,   "PowerOff", "PowerOff", null, "PowerOff",     "PowerOn",   "PowerOff"),
                ]),
                new LayoutGroupDefinitionDto("WELL",
                [
                    new CommandDefinitionDto(CommandType.TiVo, "TiVo",    "TiVo",    null, "TiVo",    null, "TiVo"),
                    new CommandDefinitionDto(CommandType.TiVo, "Netflix",  "Netflix",  null, "Netflix",  null, "Netflix"),
                    new CommandDefinitionDto(CommandType.TiVo, "Guide",    "Guide",    null, "Guide",    null, "Guide"),
                ]),
                new LayoutGroupDefinitionDto("PLAYBACK",
                [
                    new CommandDefinitionDto(CommandType.TiVo, "Play",   "Play",   null, "Play",   "Pause",  "Play"),
                    new CommandDefinitionDto(CommandType.TiVo, "Pause",  "Pause",  null, "Pause",  "Play",   "Pause"),
                    new CommandDefinitionDto(CommandType.TiVo, "Record", "Record", null, "Record", null,     "Record"),
                    new CommandDefinitionDto(CommandType.TiVo, "Skip",   "Skip",   null, "Skip",   "Replay", "Skip"),
                    new CommandDefinitionDto(CommandType.TiVo, "Replay", "Replay", null, "Replay", "Skip",   "Replay"),
                ]),
                new LayoutGroupDefinitionDto("CHANNELANDVOLUME",
                [
                    new CommandDefinitionDto(CommandType.TiVo, "ChannelUp",   "Up",   null, "Channel Up",   "ChannelDown", "ChannelUp"),
                    new CommandDefinitionDto(CommandType.TiVo, "ChannelDown", "Down", null, "Channel Down", "ChannelUp",   "ChannelDown"),
                    new CommandDefinitionDto(CommandType.IR,   "VolumeUp",    "Up",   null, "Volume Up",    "VolumeDown",  "VolumeUp"),
                    new CommandDefinitionDto(CommandType.IR,   "VolumeDown",  "Down", null, "Volume Down",  "VolumeUp",    "VolumeDown"),
                    new CommandDefinitionDto(CommandType.IR,   "Mute",        "Mute", null, "Mute",         "Mute",        "Mute"),
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
