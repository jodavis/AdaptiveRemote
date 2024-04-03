using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Impl;

internal class StaticCommandGroupProvider : IRemoteDefinitionService
{
    public LayoutGroup Root { get; } = new LayoutGroup(string.Empty, "ROOT", new List<RemoteLayoutElement>
    {
        new LayoutGroup("ROOT", "DPAD", new List<RemoteLayoutElement>
        {
            new TiVoCommand("DPAD", "Up"),
            new TiVoCommand("DPAD", "Down"),
            new TiVoCommand("DPAD", "Left"),
            new TiVoCommand("DPAD", "Right"),
            new TiVoCommand("DPAD", "Select"),
            new TiVoCommand("DPAD", "Back"),
        }),
        new LayoutGroup("ROOT", "WELL", new List<RemoteLayoutElement>
        {
            new TiVoCommand("WELL", "TiVo"),
            new TiVoCommand("WELL", "Netflix"),
            new TiVoCommand("WELL", "Guide"),
        }),
        new LayoutGroup("ROOT", "PLAYBACK", new List<RemoteLayoutElement>
        {
            new TiVoCommand("PLAYBACK", "Play"),
            new TiVoCommand("PLAYBACK", "Pause"),
            new TiVoCommand("PLAYBACK", "Record"),
            new TiVoCommand("PLAYBACK", "Skip"),
            new TiVoCommand("PLAYBACK", "Replay"),
        }),
        new LayoutGroup("ROOT", "CHANNEL", new List<RemoteLayoutElement>
        {
            new TiVoCommand("CHANNEL", "Up", "CHANNELUP"),
            new TiVoCommand("CHANNEL", "Down", "CHANNELDOWN"),
        }),
        new LayoutGroup("ROOT", "GUTTER", new List<RemoteLayoutElement>
        {
            new ExitCommand("GUTTER")
        })
    });

    IEnumerable<string> IRemoteDefinitionService.RemoteNames { get; } = new[] { "TiVo" };

    IEnumerable<Command> IRemoteDefinitionService.GetCommands(string remoteName)
    {
        throw new NotImplementedException();
    }

    RemoteLayoutElement IRemoteDefinitionService.GetRoot(string remoteName) => Root;
}
