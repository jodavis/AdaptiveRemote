namespace AdaptiveRemote.Contracts;

/// <summary>
/// LayoutCompilerService — stateless; called by LayoutProcessingService to compile a raw layout
/// into a compiled layout, and by RawLayoutService to produce a live preview.
/// </summary>
public interface ILayoutCompilerClient
{
    Task<CompiledLayout> CompileAsync(RawLayout raw, CancellationToken ct);
    Task<PreviewLayout> CompilePreviewAsync(IReadOnlyList<RawLayoutElementDto> elements, CancellationToken ct);
}
