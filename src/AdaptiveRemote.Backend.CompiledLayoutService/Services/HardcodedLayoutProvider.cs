using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.CompiledLayoutService.Services;

/// <summary>
/// Hardcoded implementation of ICompiledLayoutRepository for ADR-167 Static layout MVP.
/// Returns a fixed layout matching the current StaticCommandGroupProvider.
/// Will be replaced with real DynamoDB storage in ADR-173.
/// </summary>
public class HardcodedLayoutProvider : ICompiledLayoutRepository
{
    // Hardcoded layout ID and user ID for MVP
    private static readonly Guid LayoutId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid RawLayoutId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public Task<CompiledLayout?> GetActiveForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        List<LayoutElementDto> elements = new List<LayoutElementDto>
        {
            new LayoutGroupDefinitionDto(
                CssId: "DPAD",
                Children: new List<LayoutElementDto>
                {
                    new CommandDefinitionDto(CommandType.TiVo, "Up", "Up", null, "Sent Up", "Down", "UP"),
                    new CommandDefinitionDto(CommandType.TiVo, "Down", "Down", null, "Sent Down", "Up", "DOWN"),
                    new CommandDefinitionDto(CommandType.TiVo, "Left", "Left", null, "Sent Left", "Right", "LEFT"),
                    new CommandDefinitionDto(CommandType.TiVo, "Right", "Right", null, "Sent Right", "Left", "RIGHT"),
                    new CommandDefinitionDto(CommandType.TiVo, "Select", "Select", null, "Sent Select", null, "SELECT"),
                    new CommandDefinitionDto(CommandType.TiVo, "Back", "Back", null, "Sent Back", null, "BACK"),
                    new CommandDefinitionDto(CommandType.IR, "Power", "Power", null, "Sent Power", "Power", "POWER"),
                    new CommandDefinitionDto(CommandType.IR, "PowerOn", "PowerOn", null, "Sent PowerOn", "PowerOff", "POWERON"),
                    new CommandDefinitionDto(CommandType.IR, "PowerOff", "PowerOff", null, "Sent PowerOff", "PowerOn", "POWEROFF"),
                }.AsReadOnly()
            ),
            new LayoutGroupDefinitionDto(
                CssId: "WELL",
                Children: new List<LayoutElementDto>
                {
                    new CommandDefinitionDto(CommandType.TiVo, "TiVo", "TiVo", null, "Sent TiVo", null, "TIVO"),
                    new CommandDefinitionDto(CommandType.TiVo, "Netflix", "Netflix", null, "Sent Netflix", null, "NETFLIX"),
                    new CommandDefinitionDto(CommandType.TiVo, "Guide", "Guide", null, "Sent Guide", null, "GUIDE"),
                }.AsReadOnly()
            ),
            new LayoutGroupDefinitionDto(
                CssId: "PLAYBACK",
                Children: new List<LayoutElementDto>
                {
                    new CommandDefinitionDto(CommandType.TiVo, "Play", "Play", null, "Sent Play", "Pause", "PLAY"),
                    new CommandDefinitionDto(CommandType.TiVo, "Pause", "Pause", null, "Sent Pause", "Play", "PAUSE"),
                    new CommandDefinitionDto(CommandType.TiVo, "Record", "Record", null, "Sent Record", null, "RECORD"),
                    new CommandDefinitionDto(CommandType.TiVo, "Skip", "Skip", null, "Sent Skip", "Replay", "SKIP"),
                    new CommandDefinitionDto(CommandType.TiVo, "Replay", "Replay", null, "Sent Replay", "Skip", "REPLAY"),
                }.AsReadOnly()
            ),
            new LayoutGroupDefinitionDto(
                CssId: "CHANNELANDVOLUME",
                Children: new List<LayoutElementDto>
                {
                    new CommandDefinitionDto(CommandType.TiVo, "ChannelUp", "Up", null, "Sent Channel Up", "ChannelDown", "CHANNELUP"),
                    new CommandDefinitionDto(CommandType.TiVo, "ChannelDown", "Down", null, "Sent Channel Down", "ChannelUp", "CHANNELDOWN"),
                    new CommandDefinitionDto(CommandType.IR, "VolumeUp", "Up", null, "Sent Volume Up", "VolumeDown", "VOLUMEUP"),
                    new CommandDefinitionDto(CommandType.IR, "VolumeDown", "Down", null, "Sent Volume Down", "VolumeUp", "VOLUMEDOWN"),
                    new CommandDefinitionDto(CommandType.IR, "Mute", "Mute", null, "Sent Mute", "Mute", "MUTE"),
                }.AsReadOnly()
            ),
            new LayoutGroupDefinitionDto(
                CssId: "GUTTER",
                Children: new List<LayoutElementDto>
                {
                    new CommandDefinitionDto(CommandType.Lifecycle, "Learn", "Learn", null, "Sent Learn", null, "LEARN"),
                    new CommandDefinitionDto(CommandType.Lifecycle, "Exit", "Exit", null, "Goodbye", null, "EXIT"),
                }.AsReadOnly()
            ),
        };

        CompiledLayout layout = new CompiledLayout(
            Id: LayoutId,
            RawLayoutId: RawLayoutId,
            UserId: userId,
            IsActive: true,
            Version: 1,
            Elements: elements.AsReadOnly(),
            CssDefinitions: "/* Placeholder CSS - real CSS generation in ADR-171 */",
            CompiledAt: DateTimeOffset.UtcNow
        );

        return Task.FromResult<CompiledLayout?>(layout);
    }

    public Task<IReadOnlyList<CompiledLayout>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Hardcoded MVP — real DynamoDB implementation in ADR-173
        IReadOnlyList<CompiledLayout> empty = Array.Empty<CompiledLayout>();
        return Task.FromResult(empty);
    }

    public Task<CompiledLayout?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Hardcoded MVP — real DynamoDB implementation in ADR-173
        return Task.FromResult<CompiledLayout?>(null);
    }

    public Task<CompiledLayout> SaveAsync(CompiledLayout layout, CancellationToken cancellationToken = default)
    {
        // Hardcoded MVP — real DynamoDB implementation in ADR-173
        return Task.FromResult(layout);
    }

    public Task SetActiveAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        // Hardcoded MVP — real DynamoDB implementation in ADR-173
        return Task.CompletedTask;
    }
}
