using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Commands;

internal class StaticCommandGroupProvider : IRemoteDefinitionService
{
    public RemoteLayoutElement RemoteRoot { get; } = new LayoutGroup("ROOT",
    [
        new LayoutGroup("DPAD",
        [
            new TiVoCommand("Up"),
            new TiVoCommand("Down"),
            new TiVoCommand("Left"),
            new TiVoCommand("Right"),
            new TiVoCommand("Select"),
            new TiVoCommand("Back"),
        ]),
        new LayoutGroup("WELL",
        [
            new TiVoCommand("TiVo"),
            new TiVoCommand("Netflix"),
            new TiVoCommand("Guide"),
        ]),
        new LayoutGroup("PLAYBACK",
        [
            new TiVoCommand("Play"),
            new TiVoCommand("Pause"),
            new TiVoCommand("Record"),
            new TiVoCommand("Skip"),
            new TiVoCommand("Replay"),
        ]),
        new LayoutGroup("CHANNELANDVOLUME",
        [
            new TiVoCommand("ChannelUp", label: "Up"),
            new TiVoCommand("ChannelDown", label: "Down"),
            new IRCommand("VolumeUp", label: "Up", data: "JgBQAAABHpEQExETETcQFBATERMQFBETEDcRNxATEDcSNhE3EDcQNxEUEDcQExETEBQQExEUEBMRNxATEDcSNxA3EDcQNxE3EQAFBwABH0gQAA0F"),
            new IRCommand("VolumeDown", label: "Down", data: "JgBQAAABHpIQExETEDcQFBESEhMQExETEDcQNxITEDcQNxE2EjcQNxA3EDcRExETERMQExETEBQRExAUEDcQNxA3EjYRNxA3EAAFCAABH0gQAA0F"),
            new IRCommand("Mute", label: "Mute", data: "JgBQAAABH5ERExATETcQExEUEBMRExAUEDcRNxATETcQNxE3EDcQNxA3ERMRFBA3EBMRExATEhMQFBA3EDcQFBE3EDcQNxA3EQAFBwABH0gRAA0F"),
        ]),
        new LayoutGroup("GUTTER",
        [
            new Models.ConversationView(),
            new ApplicationCommand("Exit")
        ])
    ]);
}
