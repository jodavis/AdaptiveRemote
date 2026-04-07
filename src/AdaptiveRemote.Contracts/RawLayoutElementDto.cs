using System.Text.Json.Serialization;

namespace AdaptiveRemote.Contracts;

// ---------------------------------------------------------------------------
// Raw layout element DTOs
// Shared between the editor application (serialization) and LayoutCompilerService
// (deserialization). Extends behavioral properties with authoring properties that
// the compiler resolves into CssDefinitions and strips from the compiled output.
// ---------------------------------------------------------------------------

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RawCommandDefinitionDto), "command")]
[JsonDerivedType(typeof(RawLayoutGroupDefinitionDto), "group")]
public abstract record RawLayoutElementDto(
    string CssId,
    int GridRow,
    int GridColumn,
    int GridRowSpan = 1,
    int GridColumnSpan = 1,
    string? AdditionalCss = null    // per-element CSS overrides (e.g. red background for Power)
);

public record RawCommandDefinitionDto(
    CommandType Type,
    string Name,
    string Label,
    string? Glyph,
    string SpeakPhrase,
    string? Reverse,
    string CssId,
    int GridRow,
    int GridColumn,
    int GridRowSpan = 1,
    int GridColumnSpan = 1,
    string? AdditionalCss = null
) : RawLayoutElementDto(CssId, GridRow, GridColumn, GridRowSpan, GridColumnSpan, AdditionalCss),
    ICommandProperties;

public record RawLayoutGroupDefinitionDto(
    string CssId,
    IReadOnlyList<RawLayoutElementDto> Children,
    int GridRow,
    int GridColumn,
    int GridRowSpan = 1,
    int GridColumnSpan = 1,
    string? AdditionalCss = null
) : RawLayoutElementDto(CssId, GridRow, GridColumn, GridRowSpan, GridColumnSpan, AdditionalCss);
