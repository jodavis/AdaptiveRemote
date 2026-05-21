using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Services;

/// <summary>
/// Stub implementation of ILayoutValidationClient.
/// Returns a valid ValidationResult for all inputs by default.
/// Returns an invalid result when <see cref="CompiledLayout.CssDefinitions"/> contains
/// ".INVALID_", which E2E tests trigger by giving a layout element a cssId starting with
/// "INVALID_". The real LayoutCompilerService emits that prefix verbatim in CSS.
/// To be replaced with a real Lambda-backed HTTP client in ADR-172.
/// </summary>
public class StubLayoutValidationClient : ILayoutValidationClient
{
    public Task<ValidationResult> ValidateAsync(CompiledLayout compiled, CancellationToken ct)
    {
        // E2E tests trigger the failure path by giving an element a cssId that starts with
        // "INVALID_". The real LayoutCompilerService emits ".INVALID_<id> { ... }" in CSS,
        // which this stub detects to return a failure result without any production-code changes.
        if (compiled.CssDefinitions.Contains(".INVALID_", StringComparison.Ordinal))
        {
            ValidationResult failure = new(
                IsValid: false,
                Issues: [new ValidationIssue("STUB_INVALID", "Stub validation forced invalid for testing", null)]
            );
            return Task.FromResult(failure);
        }

        // Stub: always valid until real validation Lambda is wired in ADR-172
        ValidationResult result = new(
            IsValid: true,
            Issues: Array.Empty<ValidationIssue>()
        );

        return Task.FromResult(result);
    }
}
