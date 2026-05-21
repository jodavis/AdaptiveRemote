using System.Text;
using AdaptiveRemote.Backend.Common.Logging;
using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.LayoutCompilerService.Services;

/// <summary>
/// Compiles RawLayout and element lists into their compiled representations.
/// Stateless; all methods are synchronous pure functions over the input data.
/// </summary>
public class LayoutCompiler
{
    private readonly ILogger<LayoutCompiler> _logger;

    public LayoutCompiler(ILogger<LayoutCompiler> logger)
    {
        _logger = logger;
    }

    public CompiledLayout Compile(RawLayout raw)
    {
        _logger.CompilationStarted(raw.Id);

        IReadOnlyList<LayoutElementDto> elements = ConvertElements(raw.Elements);
        string cssDefinitions = GenerateCssDefinitions(raw.Elements);

        CompiledLayout result = new(
            Id: Guid.NewGuid(),
            RawLayoutId: raw.Id,
            UserId: raw.UserId,
            IsActive: false,
            Version: raw.Version,
            Elements: elements,
            CssDefinitions: cssDefinitions,
            CompiledAt: DateTimeOffset.UtcNow
        );

        _logger.CompilationCompleted(raw.Id);
        return result;
    }

    public PreviewLayout CompilePreview(IReadOnlyList<RawLayoutElementDto> elements)
    {
        _logger.PreviewCompilationStarted();

        string renderedHtml = GeneratePreviewHtml(elements);
        string renderedCss = GeneratePreviewCss(elements);

        PreviewLayout result = new(
            RawLayoutId: Guid.Empty,
            Version: 0,
            RenderedHtml: renderedHtml,
            RenderedCss: renderedCss,
            CompiledAt: DateTimeOffset.UtcNow,
            ValidationResult: new ValidationResult(true, Array.Empty<ValidationIssue>())
        );

        _logger.PreviewCompilationCompleted();
        return result;
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<LayoutElementDto> ConvertElements(IReadOnlyList<RawLayoutElementDto> rawElements)
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

    private static string GenerateCssDefinitions(IReadOnlyList<RawLayoutElementDto> elements)
    {
        StringBuilder sb = new();
        foreach (RawLayoutElementDto element in elements)
        {
            AppendElementCss(sb, element);
        }
        return sb.ToString().TrimEnd();
    }

    private static void AppendElementCss(StringBuilder sb, RawLayoutElementDto element)
    {
        sb.Append('.').Append(element.CssId).Append(" { ");
        sb.Append("grid-row: ").Append(element.GridRow).Append(" / span ").Append(element.GridRowSpan).Append("; ");
        sb.Append("grid-column: ").Append(element.GridColumn).Append(" / span ").Append(element.GridColumnSpan).Append(';');

        if (!string.IsNullOrEmpty(element.AdditionalCss))
        {
            sb.Append(' ').Append(element.AdditionalCss.TrimEnd(';')).Append(';');
        }

        sb.AppendLine(" }");

        if (element is RawLayoutGroupDefinitionDto group)
        {
            foreach (RawLayoutElementDto child in group.Children)
            {
                AppendElementCss(sb, child);
            }
        }
    }

    private static string GeneratePreviewHtml(IReadOnlyList<RawLayoutElementDto> elements)
    {
        StringBuilder sb = new();
        sb.AppendLine("<div class=\"layout-preview-grid\">");
        foreach (RawLayoutElementDto element in elements)
        {
            AppendElementHtml(sb, element, indent: 2);
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    private static void AppendElementHtml(StringBuilder sb, RawLayoutElementDto element, int indent)
    {
        string pad = new(' ', indent);
        switch (element)
        {
            case RawCommandDefinitionDto cmd:
                sb.AppendLine($"{pad}<div class=\"{cmd.CssId}\"><span>{EscapeHtml(cmd.Label)}</span></div>");
                break;
            case RawLayoutGroupDefinitionDto group:
                sb.AppendLine($"{pad}<div class=\"{group.CssId}\">");
                foreach (RawLayoutElementDto child in group.Children)
                {
                    AppendElementHtml(sb, child, indent + 2);
                }
                sb.AppendLine($"{pad}</div>");
                break;
        }
    }

    private static string GeneratePreviewCss(IReadOnlyList<RawLayoutElementDto> elements)
    {
        StringBuilder sb = new();
        sb.AppendLine(".layout-preview-grid { display: grid; }");
        foreach (RawLayoutElementDto element in elements)
        {
            AppendElementCss(sb, element);
        }
        return sb.ToString().TrimEnd();
    }

    private static string EscapeHtml(string text)
        => text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}
