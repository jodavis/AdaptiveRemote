namespace AdaptiveRemote.Contracts;

/// <summary>
/// LayoutValidationService — stateless; called by LayoutProcessingService to validate a
/// compiled layout before storing it.
/// </summary>
public interface ILayoutValidationClient
{
    Task<ValidationResult> ValidateAsync(CompiledLayout compiled, CancellationToken ct);
}
