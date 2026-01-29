using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndtoEndTests.SimulatedTiVo;

/// <summary>
/// Builder for creating and starting a SimulatedTiVoDevice.
/// </summary>
public sealed class SimulatedTiVoDeviceBuilder : ISimulatedDeviceBuilder
{
    private readonly ILogger _logger;
    private int _port = 0; // Default to ephemeral port

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulatedTiVoDeviceBuilder"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating the device logger.</param>
    public SimulatedTiVoDeviceBuilder(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SimulatedTiVoDevice>();
    }

    /// <inheritdoc/>
    public ISimulatedDeviceBuilder WithPort(int port)
    {
        _port = port;
        return this;
    }

    /// <inheritdoc/>
    public ISimulatedTiVoDevice Start()
    {
        return new SimulatedTiVoDevice(_port, _logger);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // No resources to dispose in builder
    }
}
