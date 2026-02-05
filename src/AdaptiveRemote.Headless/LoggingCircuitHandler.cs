using AdaptiveRemote.Headless.Logging;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace AdaptiveRemote.Headless;

/// <summary>
/// Diagnostic hooks to make sure Blazor circuits are initialized correctly
/// </summary>
internal class LoggingCircuitHandler : CircuitHandler
{
    private readonly HeadlessHostMessageLogger _logger;

    public LoggingCircuitHandler(ILogger<LoggingCircuitHandler> logger)
    {
        _logger = new(logger);
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.CircuitHandler_Opened(circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.CircuitHandler_Closed(circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.CircuitHandler_ConnectionUp(circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.CircuitHandler_ConnectionDown(circuit.Id);
        return Task.CompletedTask;
    }
}
