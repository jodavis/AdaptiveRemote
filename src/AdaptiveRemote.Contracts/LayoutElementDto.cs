using System.Text.Json.Serialization;

namespace AdaptiveRemote.Contracts;

// ---------------------------------------------------------------------------
// Compiled layout element DTOs
// Used in CompiledLayout.Elements. Deserialized directly by the client application.
// Contains only behavioral properties — grid positions and CSS overrides have been
// compiled into CssDefinitions and are not needed by the client.
// ---------------------------------------------------------------------------

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CommandDefinitionDto), "command")]
[JsonDerivedType(typeof(LayoutGroupDefinitionDto), "group")]
public abstract record LayoutElementDto(string CssId);

// Maps to AdaptiveRemote.App.Models.Command at layout-apply time (client epic).
// Type carries the CommandType discriminator so the client knows which runtime type to instantiate.
// No subtype hierarchy is used — all behavioral properties are flat; type-specific execution
// parameters are resolved by the client from its own configuration (see CommandType above).
public record CommandDefinitionDto(
    CommandType Type,
    string Name,
    string Label,
    string? Glyph,
    string SpeakPhrase,
    string? Reverse,
    string CssId
) : LayoutElementDto(CssId), ICommandProperties;

// Maps to AdaptiveRemote.App.Models.LayoutGroup at layout-apply time (client epic).
public record LayoutGroupDefinitionDto(
    string CssId,
    IReadOnlyList<LayoutElementDto> Children
) : LayoutElementDto(CssId);
