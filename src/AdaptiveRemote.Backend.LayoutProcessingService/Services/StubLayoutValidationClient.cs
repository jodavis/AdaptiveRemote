using AdaptiveRemote.Contracts;
using Microsoft.Extensions.Configuration;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Services;

/// <summary>
/// Stub implementation of ILayoutValidationClient.
/// Returns a valid ValidationResult for all inputs by default.
/// When Validation:ForceInvalid is set to true (e.g. in integration tests), returns an
/// invalid result with a single stub issue, enabling end-to-end testing of the failure path.
/// To be replaced with a real Lambda-backed HTTP client in ADR-172.
/// </summary>
public class StubLayoutValidationClient : ILayoutValidationClient
{
    private readonly bool _forceInvalid;

    public StubLayoutValidationClient(IConfiguration configuration)
    {
        _forceInvalid = configuration.GetValue("Validation:ForceInvalid", defaultValue: false);
    }

    public Task<ValidationResult> ValidateAsync(CompiledLayout compiled, CancellationToken ct)
    {
        if (_forceInvalid)
        {
            ValidationResult failure = new(
                IsValid: false,
                Issues: [new ValidationIssue("STUB_INVALID", "Stub validation forced invalid for testing", null)]
            );
            return Task.FromResult(failure);
        }

        ValidationResult result = new(
            IsValid: true,
            Issues: Array.Empty<ValidationIssue>()
        );

        return Task.FromResult(result);
    }
}
