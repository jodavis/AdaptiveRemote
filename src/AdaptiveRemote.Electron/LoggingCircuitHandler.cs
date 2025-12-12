using Microsoft.AspNetCore.Components.Server.Circuits;

namespace AdaptiveRemote.Electron;

/// <summary>
/// Diagnostic hooks to make sure Blazor circuits are initialized correctly
/// </summary>
internal class LoggingCircuitHandler : CircuitHandler
{
    private readonly ILogger<LoggingCircuitHandler> _logger;

    public LoggingCircuitHandler(ILogger<LoggingCircuitHandler> logger)
    {
        _logger = logger;
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Circuit opened: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Circuit closed: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Connection up: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Connection down: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }
}
