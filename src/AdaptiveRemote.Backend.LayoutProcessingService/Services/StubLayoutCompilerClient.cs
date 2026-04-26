using System.Collections.ObjectModel;
using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Services;

/// <summary>
/// Stub implementation of ILayoutCompilerClient.
/// Returns a hardcoded CompiledLayout derived from the input RawLayout elements
/// (names and labels pass through; no real CSS generation).
/// To be replaced with a real Lambda-backed HTTP client in ADR-171.
/// </summary>
public class StubLayoutCompilerClient : ILayoutCompilerClient
{
    public Task<CompiledLayout> CompileAsync(RawLayout raw, CancellationToken ct)
    {
        IReadOnlyList<LayoutElementDto> compiledElements = ConvertElements(raw.Elements);

        CompiledLayout compiled = new(
            Id: Guid.NewGuid(),
            RawLayoutId: raw.Id,
            UserId: raw.UserId,
            IsActive: false,
            Version: raw.Version,
            Elements: compiledElements,
            CssDefinitions: string.Empty,  // Stub: no real CSS generation until ADR-171
            CompiledAt: DateTimeOffset.UtcNow
        );

        return Task.FromResult(compiled);
    }

    public Task<PreviewLayout> CompilePreviewAsync(IReadOnlyList<RawLayoutElementDto> elements, CancellationToken ct)
    {
        PreviewLayout preview = new(
            RawLayoutId: Guid.Empty,
            Version: 0,
            RenderedHtml: "<div><!-- Stub preview --></div>",
            RenderedCss: string.Empty,
            CompiledAt: DateTimeOffset.UtcNow,
            ValidationResult: new ValidationResult(true, Array.Empty<ValidationIssue>())
        );

        return Task.FromResult(preview);
    }

    private static ReadOnlyCollection<LayoutElementDto> ConvertElements(IReadOnlyList<RawLayoutElementDto> rawElements)
    {
        List<LayoutElementDto> result = new(rawElements.Count);

        foreach (RawLayoutElementDto element in rawElements)
        {
            LayoutElementDto compiled = element switch
            {
                RawCommandDefinitionDto cmd => new CommandDefinitionDto(
                    Type: cmd.Type,
                    Name: cmd.Name,
                    Label: cmd.Label,
                    Glyph: cmd.Glyph,
                    SpeakPhrase: cmd.SpeakPhrase,
                    Reverse: cmd.Reverse,
                    CssId: cmd.CssId),
                RawLayoutGroupDefinitionDto group => new LayoutGroupDefinitionDto(
                    CssId: group.CssId,
                    Children: ConvertElements(group.Children)),
                _ => throw new InvalidOperationException($"Unknown element type: {element.GetType().Name}")
            };

            result.Add(compiled);
        }

        return result.AsReadOnly();
    }
}
