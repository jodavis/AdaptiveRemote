using PolyType;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Represents an accessibility violation detected by the accessibility checker.
/// </summary>
[GenerateShape]
public partial class AccessibilityViolation
{
    /// <summary>
    /// Gets or sets the unique identifier of the accessibility rule that was violated.
    /// </summary>
    public required string RuleId { get; set; }

    /// <summary>
    /// Gets or sets the impact level of the violation (e.g., "critical", "serious", "moderate", "minor").
    /// </summary>
    public required string Impact { get; set; }

    /// <summary>
    /// Gets or sets a description of the accessibility issue.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the HTML snippet of the element with the violation.
    /// </summary>
    public required string HtmlSnippet { get; set; }

    /// <summary>
    /// Gets or sets help text explaining how to fix the violation.
    /// </summary>
    public required string HelpText { get; set; }

    /// <summary>
    /// Gets or sets a URL to more information about the accessibility rule.
    /// </summary>
    public required string HelpUrl { get; set; }
}
