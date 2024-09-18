using System.Configuration;
using AdaptiveRemote.Configuration;
using AdaptiveRemote.Models;
using AdaptiveRemote.Services.Broadlink;

namespace AdaptiveRemote.Utilities;

internal static class Errors
{
    public static Exception Telemetry_SettingRequiredToPublish(string settingName)
        => SettingRequired(SettingsKeys.Telemetry, settingName, "publish telemetry");

    internal static Exception TiVo_SettingRequiredToConnect(string settingName)
        => SettingRequired(SettingsKeys.TiVo, settingName, "connect to the TiVo");

    internal static Exception UdpService_NetworkTimeoutError(TimeSpan timeout)
        => new UdpException($"Network timeout: No response received within {timeout.TotalSeconds}s");
    internal static Exception UdpService_ResponseTooShort(int expected, int actual)
        => new UdpException($"Received data packet length error: Expected at least {expected} bytes but received {actual} bytes");
    internal static Exception UdpService_ChecksumMismatch(short nominalChecksum, int realCheckSum)
        => new UdpException($"Received data packet check error: Expected a checksum of {nominalChecksum} but computed {realCheckSum}");

    internal static Exception DeviceLocator_DeviceNotFound()
        => new TimeoutException("No Broadlink device was found");

    internal static Exception Broadlink_UnknownError()
        => new BroadlinkException("Unknown error");
    internal static Exception Broadlink_AuthenticationFailed()
        => new BroadlinkException("Authentication failed");
    internal static Exception Broadlink_Loggedout()
        => new BroadlinkException("You have been logged out");
    internal static Exception Broadlink_StorageError()
        => new BroadlinkException("The device storage is full");
    internal static Exception Broadlink_DeviceOffline()
        => new BroadlinkException("The device is offline");
    internal static Exception Broadlink_CommandNotSupported()
        => new BroadlinkException("Command not supported");
    internal static Exception Broadlink_AbnormalStructure()
        => new BroadlinkException("Structure is abnormal");
    internal static Exception Broadlink_ControlKeyExpired()
        => new BroadlinkException("Control key is expired");
    internal static Exception Broadlink_SendError()
        => new BroadlinkException("Send error");
    internal static Exception Broadlink_WriteError()
        => new BroadlinkException("Write error");
    internal static Exception Broadlink_ReadError()
        => new BroadlinkException("Read error");
    internal static Exception Broadlink_SsidNotFound()
        => new BroadlinkException("SSID could not be found in AP configuration");

    internal static Exception CommandService_NotStarted(Command command)
        => new InvalidOperationException($"Cannot execute {command} because the service has not been started.");
    internal static Exception CommandService_WasShutDown(Command command)
        => new InvalidOperationException($"Cannot execute {command} because the service has been shut down.");

    internal static Exception PersistSettings_InvalidName(string paramName, string settingName)
        => new ArgumentException($"The setting name '{settingName}' was in an invalid format.", paramName);
    internal static Exception PersistSettings_InvalidValue(string paramName, string settingValue)
        => new ArgumentException($"The setting value '{settingValue}' was in an invalid format.", paramName);

    private static ConfigurationErrorsException SettingRequired(string settingKey, string settingName, string requiredTo)
        => new($"The '{settingKey}:{settingName}' setting is required to {requiredTo}");
}
