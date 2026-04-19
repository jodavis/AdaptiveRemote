using AdaptiveRemote.Contracts;
using AdaptiveRemote.Models;
using AdaptiveRemote.Services.CloudAssets;

namespace AdaptiveRemote.Services.Layout;

/// <summary>
/// RemoteLayoutDefinitionService v2: reads CompiledLayout from ICloudAssetStore,
/// maps the element tree to runtime types per the DTO mapping table, and appends
/// a hardcoded GUTTER as the last root child.
/// </summary>
internal class RemoteLayoutDefinitionService : IRemoteDefinitionService
{
    private readonly ICloudAssetStore _store;

    public RemoteLayoutDefinitionService(ICloudAssetStore store)
    {
        _store = store;
    }

    public RemoteLayoutElement RemoteRoot
    {
        get
        {
            CompiledLayout layout;
            try
            {
                layout = _store.GetLayout();
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    "Failed to load layout from CloudAssetStore. " +
                    "Ensure CloudAssetOrchestrator has completed initialization before the first scope is created.",
                    ex);
            }

            List<RemoteLayoutElement> elements = layout.Elements
                .Select(MapElement)
                .ToList();
            elements.Add(BuildGutter());

            return new LayoutGroup("ROOT", elements);
        }
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
                speakName: cmd.SpeakPhrase),

            CommandType.IR => new IRCommand(
                cmd.Name, placement: null, label: cmd.Label,
                cssid: cmd.CssId, glyph: cmd.Glyph, reverse: cmd.Reverse,
                speakName: cmd.SpeakPhrase),

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
