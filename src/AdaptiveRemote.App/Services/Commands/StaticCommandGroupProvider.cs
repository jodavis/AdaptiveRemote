using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Commands;

internal class StaticCommandGroupProvider : IRemoteDefinitionService
{
    public RemoteLayoutElement RemoteRoot { get; } = new LayoutGroup("ROOT",
    [
        new LayoutGroup("DPAD",
        [
            new TiVoCommand("Up", reverse: "Down"),
            new TiVoCommand("Down", reverse: "Up"),
            new TiVoCommand("Left", reverse: "Right"),
            new TiVoCommand("Right", reverse: "Left"),
            new TiVoCommand("Select"),
            new TiVoCommand("Back"),
            new IRCommand("Power", reverse: "Power"),
            new IRCommand("PowerOn", reverse: "PowerOff"),
            new IRCommand("PowerOff", reverse: "PowerOn"),
        ]),
        new LayoutGroup("WELL",
        [
            new TiVoCommand("TiVo"),
            new TiVoCommand("Netflix"),
            new TiVoCommand("Guide"),
        ]),
        new LayoutGroup("PLAYBACK",
        [
            new TiVoCommand("Play", reverse: "Pause"),
            new TiVoCommand("Pause", reverse: "Play"),
            new TiVoCommand("Record"),
            new TiVoCommand("Skip", reverse: "Replay"),
            new TiVoCommand("Replay", reverse: "Skip"),
        ]),
        new LayoutGroup("CHANNELANDVOLUME",
        [
            new TiVoCommand("ChannelUp", reverse: "ChannelDown", label: "Up", speakName: "Channel Up"),
            new TiVoCommand("ChannelDown", reverse: "ChannelUp", label: "Down", speakName: "Channel Down"),
            new IRCommand("VolumeUp", reverse: "VolumeDown", label: "Up", speakName: "Volume Up"),
            new IRCommand("VolumeDown", reverse: "VolumeUp", label: "Down", speakName: "Volume Down"),
            new IRCommand("Mute", reverse: "Mute", label: "Mute"),
        ]),
        new LayoutGroup("GUTTER",
        [
            new ConversationView(),
            new LifecycleCommand("Learn"),
            new LifecycleCommand("Exit", speakPhrase: Phrases.Conversation_Goodbye)
        ])
    ]);
}
