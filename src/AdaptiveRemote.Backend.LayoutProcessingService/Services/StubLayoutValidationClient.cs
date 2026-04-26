using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Services;

/// <summary>
/// Stub implementation of ILayoutValidationClient.
/// Returns a valid ValidationResult for all inputs.
/// To be replaced with a real Lambda-backed HTTP client in ADR-172.
/// </summary>
public class StubLayoutValidationClient : ILayoutValidationClient
{
    public Task<ValidationResult> ValidateAsync(CompiledLayout compiled, CancellationToken ct)
    {
        // Stub: always valid until real validation Lambda is wired in ADR-172
        ValidationResult result = new(
            IsValid: true,
            Issues: Array.Empty<ValidationIssue>()
        );

        return Task.FromResult(result);
    }
}
