namespace AdaptiveRemote.Contracts;

// ---------------------------------------------------------------------------
// Top-level layout records
// ---------------------------------------------------------------------------

// Administrator-editable source format. Elements are typed; no opaque JSON string.
public record RawLayout(
    Guid Id,
    string UserId,
    string Name,
    IReadOnlyList<RawLayoutElementDto> Elements,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ValidationResult? ValidationResult    // written by LayoutProcessingService via IRawLayoutStatusWriter
);

// Client-consumable format produced by LayoutCompilerService.
// Deserialized directly by the client application — no intermediate parsing model needed.
// The client maps Elements → runtime Command objects at layout-apply time (client epic).
public record CompiledLayout(
    Guid Id,
    Guid RawLayoutId,
    string UserId,
    bool IsActive,
    int Version,
    IReadOnlyList<LayoutElementDto> Elements,
    string CssDefinitions,                // global CSS for the layout grid
    DateTimeOffset CompiledAt
);

// Editor-consumable preview format, produced by LayoutCompilerService.
public record PreviewLayout(
    Guid RawLayoutId,
    int Version,
    string RenderedHtml,
    string RenderedCss,
    DateTimeOffset CompiledAt,
    ValidationResult ValidationResult
);
