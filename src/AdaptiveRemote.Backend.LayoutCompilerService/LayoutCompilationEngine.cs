using System.Collections.ObjectModel;
using System.Text;
using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.LayoutCompilerService;

/// <summary>
/// Pure, stateless compilation logic: raw layout elements → compiled layout + CSS definitions.
///
/// CSS Grid approach:
///   - The grid container uses a class selector (.layout-grid) and defines its column/row
///     template based on the maximum observed column/row extents in the element set.
///   - Each element is targeted by its CssId:  #cssId { grid-row: R / span S; grid-column: C / span S; }
///   - Per-element AdditionalCss (if any) is appended inline under the same selector block.
///
/// Compiled output strips all authoring properties (grid positions, AdditionalCss); only
/// behavioral properties (Type, Name, Label, Glyph, SpeakPhrase, Reverse, CssId) are kept.
/// </summary>
public static class LayoutCompilationEngine
{
    /// <summary>
    /// Compiles a <see cref="RawLayout"/> into a <see cref="CompiledLayout"/>.
    /// Version is inherited from <paramref name="raw"/>; Id is newly generated.
    /// </summary>
    public static CompiledLayout Compile(RawLayout raw)
    {
        string css = BuildCssDefinitions(raw.Elements);
        IReadOnlyList<LayoutElementDto> elements = ConvertElements(raw.Elements);

        return new CompiledLayout(
            Id: Guid.NewGuid(),
            RawLayoutId: raw.Id,
            UserId: raw.UserId,
            IsActive: false,
            Version: raw.Version,
            Elements: elements,
            CssDefinitions: css,
            CompiledAt: DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Compiles a flat list of <see cref="RawLayoutElementDto"/> for live preview.
    /// Returns HTML + CSS representations without a full layout context.
    /// </summary>
    public static PreviewLayout CompilePreview(IReadOnlyList<RawLayoutElementDto> elements)
    {
        string css = BuildCssDefinitions(elements);
        string html = BuildPreviewHtml(elements);

        return new PreviewLayout(
            RawLayoutId: Guid.Empty,
            Version: 0,
            RenderedHtml: html,
            RenderedCss: css,
            CompiledAt: DateTimeOffset.UtcNow,
            ValidationResult: new ValidationResult(true, Array.Empty<ValidationIssue>()));
    }

    // ── CSS generation ────────────────────────────────────────────────────────

    internal static string BuildCssDefinitions(IReadOnlyList<RawLayoutElementDto> elements)
    {
        (int maxCol, int maxRow) = ComputeGridExtents(elements);

        StringBuilder sb = new();

        // Container rule — sets up an explicit grid sized to the observed extents.
        sb.AppendLine(".layout-grid {");
        sb.AppendLine("  display: grid;");
        sb.AppendLine($"  grid-template-columns: repeat({maxCol}, 1fr);");
        sb.AppendLine($"  grid-template-rows: repeat({maxRow}, auto);");
        sb.AppendLine("}");

        AppendElementCssRules(sb, elements);

        return sb.ToString();
    }

    private static void AppendElementCssRules(StringBuilder sb, IReadOnlyList<RawLayoutElementDto> elements)
    {
        foreach (RawLayoutElementDto element in elements)
        {
            sb.AppendLine();
            sb.AppendLine($"#{element.CssId} {{");
            sb.AppendLine($"  grid-row: {element.GridRow} / span {element.GridRowSpan};");
            sb.AppendLine($"  grid-column: {element.GridColumn} / span {element.GridColumnSpan};");

            if (!string.IsNullOrWhiteSpace(element.AdditionalCss))
            {
                // Inline per-element overrides inside the same rule block.
                foreach (string line in element.AdditionalCss.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    sb.AppendLine($"  {line.Trim()}");
                }
            }

            sb.AppendLine("}");

            // Recurse into groups.
            if (element is RawLayoutGroupDefinitionDto group)
            {
                AppendElementCssRules(sb, group.Children);
            }
        }
    }

    /// <summary>
    /// Returns (maxEndColumn, maxEndRow) based on the grid positions + spans of all elements
    /// (recursively).  Values are 1-based to match CSS Grid semantics.
    /// </summary>
    private static (int MaxEndColumn, int MaxEndRow) ComputeGridExtents(IReadOnlyList<RawLayoutElementDto> elements)
    {
        int maxCol = 1;
        int maxRow = 1;
        AccumulateExtents(elements, ref maxCol, ref maxRow);
        return (maxCol, maxRow);
    }

    private static void AccumulateExtents(IReadOnlyList<RawLayoutElementDto> elements, ref int maxCol, ref int maxRow)
    {
        foreach (RawLayoutElementDto e in elements)
        {
            int endCol = e.GridColumn + e.GridColumnSpan - 1;
            int endRow = e.GridRow + e.GridRowSpan - 1;
            if (endCol > maxCol)
            {
                maxCol = endCol;
            }

            if (endRow > maxRow)
            {
                maxRow = endRow;
            }

            if (e is RawLayoutGroupDefinitionDto group)
            {
                AccumulateExtents(group.Children, ref maxCol, ref maxRow);
            }
        }
    }

    // ── Element conversion ────────────────────────────────────────────────────

    internal static ReadOnlyCollection<LayoutElementDto> ConvertElements(IReadOnlyList<RawLayoutElementDto> rawElements)
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

    // ── HTML preview ──────────────────────────────────────────────────────────

    private static string BuildPreviewHtml(IReadOnlyList<RawLayoutElementDto> elements)
    {
        StringBuilder sb = new();
        sb.AppendLine("<div class=\"layout-grid\">");
        BuildPreviewHtmlChildren(sb, elements, indent: 2);
        sb.Append("</div>");
        return sb.ToString();
    }

    private static void BuildPreviewHtmlChildren(StringBuilder sb, IReadOnlyList<RawLayoutElementDto> elements, int indent)
    {
        string pad = new(' ', indent);
        foreach (RawLayoutElementDto element in elements)
        {
            switch (element)
            {
                case RawCommandDefinitionDto cmd:
                    sb.AppendLine($"{pad}<button id=\"{HtmlEncode(cmd.CssId)}\" class=\"layout-command\">{HtmlEncode(cmd.Label)}</button>");
                    break;

                case RawLayoutGroupDefinitionDto group:
                    sb.AppendLine($"{pad}<div id=\"{HtmlEncode(group.CssId)}\" class=\"layout-group\">");
                    BuildPreviewHtmlChildren(sb, group.Children, indent + 2);
                    sb.AppendLine($"{pad}</div>");
                    break;
            }
        }
    }

    /// <summary>Minimal HTML encoding — covers the characters that matter in attribute values and text content.</summary>
    private static string HtmlEncode(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
