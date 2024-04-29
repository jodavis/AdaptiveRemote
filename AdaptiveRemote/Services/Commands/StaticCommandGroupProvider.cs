using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Impl;

internal class StaticCommandGroupProvider : IRemoteDefinitionService
{
    public RemoteLayoutElement RemoteRoot { get; } = new LayoutGroup("ROOT", new List<RemoteLayoutElement>
    {
        new LayoutGroup("DPAD", new List<RemoteLayoutElement>
        {
            new TiVoCommand("Up"),
            new TiVoCommand("Down"),
            new TiVoCommand("Left"),
            new TiVoCommand("Right"),
            new TiVoCommand("Select"),
            new TiVoCommand("Back"),
        }, "ROOT"),
        new LayoutGroup("WELL", new List<RemoteLayoutElement>
        {
            new TiVoCommand("TiVo"),
            new TiVoCommand("Netflix"),
            new TiVoCommand("Guide"),
        }, "ROOT"),
        new LayoutGroup("PLAYBACK", new List<RemoteLayoutElement>
        {
            new TiVoCommand("Play"),
            new TiVoCommand("Pause"),
            new TiVoCommand("Record"),
            new TiVoCommand("Skip"),
            new TiVoCommand("Replay"),
        }, "ROOT"),
        new LayoutGroup("CHANNEL", new List<RemoteLayoutElement>
        {
            new TiVoCommand("ChannelUp", label: "Up", commandId: "CHANNELUP"),
            new TiVoCommand("ChannelDown", label: "Down", commandId: "CHANNELDOWN"),
        }, "ROOT"),
        new LayoutGroup("GUTTER", new List<RemoteLayoutElement>
        {
            new Models.Conversation(),
            new ApplicationCommand("Exit")
        }, "ROOT")
    }, string.Empty);
}
