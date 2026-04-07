namespace AdaptiveRemote.Contracts;

public record ValidationIssue(string Code, string Message, string? Path);

public record ValidationResult(bool IsValid, IReadOnlyList<ValidationIssue> Issues);
