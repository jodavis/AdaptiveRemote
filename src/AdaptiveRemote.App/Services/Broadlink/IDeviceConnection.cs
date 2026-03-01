using System.Net;
using System.Net.NetworkInformation;

namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// Represents a connection to a Broadlink Device
/// </summary>
internal interface IDeviceConnection
{
    /// <summary>
    /// "Authenticates" with the device. This must be called before any other commands
    /// are sent.
    /// </summary>
    /// <returns>A Task that completes when the device responds to authentication</returns>
    Task<bool> AuthenticateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Send a packet of IR data that should be broadcasted by the device.
    /// </summary>
    /// <param name="data">IR signal data</param>
    /// <returns>A Task that completes when the IR signal has been sent</returns>
    Task SendDataAsync(byte[] data, CancellationToken cancellationToken);

    /// <summary>
    /// Puts the device into IR learning mode, ready to capture the next IR signal it receives.
    /// </summary>
    /// <returns>A Task that completes when the device has entered learning mode</returns>
    Task EnterLearningModeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether the device has captured an IR signal during learning mode.
    /// </summary>
    /// <returns>
    /// A Task that completes with the captured IR data bytes if an IR signal has been received,
    /// or <c>null</c> if no signal has been captured yet.
    /// </returns>
    Task<byte[]?> CheckLearnedDataAsync(CancellationToken cancellationToken);

    internal interface Factory
    {
        /// <summary>
        /// Create an IDeviceConnection for a Broadlink device
        /// </summary>
        /// <param name="hostEndPoint">IP address of the device</param>
        /// <param name="hostAddress">MAC address of the device</param>
        /// <param name="deviceType">Type identifier for the device</param>
        /// <returns></returns>
        IDeviceConnection Create(
            IPEndPoint hostEndPoint,
            PhysicalAddress hostAddress,
            short deviceType);
    }
}
