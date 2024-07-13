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
            new IRCommand("VolumeUp", label: "Up", data: "JgDwAI6SDxIOEQ8SDxEOMg8xDxIPEQ8xEDAQMQ8xDxEPERARDhIPkQ8SDhIOEg8RDzEPMg4yDhMOMg4yDzEPERARDhIPEQ8xDxEQEQ4SDzEPAAbxjpMPEQ4SDxEQEQ4yDzEPERARDjIPMQ8yDjIPEQ8RDxIPEQ6TDhEPEg8RDhIPMg4yDzEPERAwEDEPMRARDhEQEQ8RDzEQEQ4SDxEPMg4ABvKPkg8RDxEPEg4REDEPMQ8SDhEQMg4yDzEPMg4RDxIPEQ4SD5EPEg4RDxIPEQ8xEDAQMQ8RDzIOMg8xDxEQEQ4TDhEPMg4SDxEPEg4yDwANBQ=="),
            new IRCommand("VolumeDown", label: "Down", data: "JgCgAI+SDxEPERARDhIPMg4yDxEOEg8yDjIPMQ8yDhEQEQ8RDxEPkRARDxIOEQ8SDxEOEg8RDzEPMg4yDzEPEg8xDzIOMg8RDxEPERARDjIPAAbyj5IPEQ8SDhEPEg8xDzIOEQ8SDzIOMg4yDzIOERARDxEPEQ+SDxEOEw4RDxIPEQ4SDxEPMQ8yDzEQMBARDzEPMg8xDxEPEg4SDxEPMQ8ADQU="),
            new IRCommand("Mute", label: "Mute", data: "JgCgAI+RDxIPEQ4SDxEPMQ8yDhIPERAwEDEPMRAwEBEPEQ8RDxEQkQ4SDxEPEg4SDjIOEw4RDxIOMg8xDzIOERARDzEPMg4yDxEPEQ8REDAQAAbwj5IPEQ8SDhIPEQ8yDjIOERARDzEPMg4yDzEPEg8RDhIPEQ+SDhEPEg8RDhIPMg4RDxIPEQ4yDzEQMQ8RDxEQMBAxDzEQEQ4REBEPMQ8ADQU="),
        ]),
        new LayoutGroup("GUTTER",
        [
            new ConversationView(),
            new ApplicationCommand("Exit")
        ])
    ]);
}
