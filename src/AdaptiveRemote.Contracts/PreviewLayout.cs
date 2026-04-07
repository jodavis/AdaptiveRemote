namespace AdaptiveRemote.Contracts;

// Editor-consumable preview format, produced by LayoutCompilerService.
public record PreviewLayout(
    Guid RawLayoutId,
    int Version,
    string RenderedHtml,
    string RenderedCss,
    DateTimeOffset CompiledAt,
    ValidationResult ValidationResult
);
