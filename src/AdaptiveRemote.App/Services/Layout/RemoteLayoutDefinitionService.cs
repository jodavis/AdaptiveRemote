using AdaptiveRemote.Contracts;
using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Layout;

internal class RemoteLayoutDefinitionService : IRemoteDefinitionService
{
    public RemoteLayoutElement RemoteRoot { get; }

    public RemoteLayoutDefinitionService(CompiledLayout layout)
    {
        RemoteRoot = BuildRoot(layout);
    }

    private static RemoteLayoutElement BuildRoot(CompiledLayout layout)
    {
        if (layout.Elements.Any(e => e.CssId == "GUTTER"))
        {
            throw new InvalidOperationException(
                "The CompiledLayout already contains a GUTTER element. " +
                "The client appends its own GUTTER; the server must not include one.");
        }

        List<RemoteLayoutElement> elements = layout.Elements
            .Select(MapElement)
            .ToList();
        elements.Add(BuildGutter());

        return new LayoutGroup("ROOT", elements);
    }

    private static RemoteLayoutElement MapElement(LayoutElementDto dto) => dto switch
    {
        LayoutGroupDefinitionDto group => new LayoutGroup(
            group.CssId,
            group.Children.Select(MapElement).ToList()),

        CommandDefinitionDto cmd => cmd.Type switch
        {
            CommandType.TiVo => new TiVoCommand(
                cmd.Name, placement: null, label: cmd.Label,
                cssid: cmd.CssId, glyph: cmd.Glyph, reverse: cmd.Reverse,
                speakPhrase: cmd.SpeakPhrase),

            CommandType.IR => new IRCommand(
                cmd.Name, placement: null, label: cmd.Label,
                cssid: cmd.CssId, glyph: cmd.Glyph, reverse: cmd.Reverse,
                speakPhrase: cmd.SpeakPhrase),

            CommandType.Lifecycle => new LifecycleCommand(
                cmd.Name, placement: null, label: cmd.Label,
                cssid: cmd.CssId, glyph: cmd.Glyph, reverse: cmd.Reverse,
                speakPhrase: cmd.SpeakPhrase),

            _ => throw new InvalidOperationException(
                $"Unknown CommandType '{cmd.Type}' on command '{cmd.Name}'.")
        },

        _ => throw new InvalidOperationException(
            $"Unknown LayoutElementDto type '{dto.GetType().Name}'.")
    };

    private static LayoutGroup BuildGutter() =>
        new("GUTTER",
        [
            new ConversationView(),
            new LifecycleCommand("Learn"),
            new LifecycleCommand("Exit", speakPhrase: Phrases.Conversation_Goodbye),
        ]);
}
