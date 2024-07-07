using System.Configuration;
using AdaptiveRemote.Services.Broadlink;
using AdaptiveRemote.Services.Configuration;

namespace AdaptiveRemote.Models;

internal static class Errors
{
    public static Exception Telemetry_SettingRequiredToPublish(string settingName)
        => SettingRequired(SettingsKeys.Telemetry, settingName, "publish telemetry");

    public static Exception Commands_UnsupportedCommandType(Command command, string argumentName)
        => new ArgumentException($"Cannot execute {command}", argumentName);

    internal static Exception TiVo_SettingRequiredToConnect(string settingName)
        => SettingRequired(SettingsKeys.TiVo, settingName, "connect to the TiVo");
    internal static Exception TiVo_CannotInterpretCommand(string commandId, string argName)
        => new ArgumentException($"Unable to interpret '{commandId}' as a TiVo command", argName);
    internal static Exception TiVo_NotInitialized(string command)
        => new InvalidOperationException($"Could not send '{command}' to the TiVo because the connection to the TiVo was not created.");

    internal static Exception UdpService_NetworkTimeoutError(TimeSpan timeout)
        => new UdpException($"Network timeout: No response received within {timeout.TotalSeconds}s");
    internal static Exception UdpService_ResponseTooShort(int expected, int actual)
        => new UdpException($"Received data packet length error: Expected at least {expected} bytes but received {actual} bytes");
    internal static Exception UdpService_ChecksumMismatch(short nominalChecksum, int realCheckSum)
        => new UdpException($"Received data packet check error: Expected a checksum of {nominalChecksum} but computed {realCheckSum}");

    private static ConfigurationErrorsException SettingRequired(string settingKey, string settingName, string requiredTo)
        => new($"The '{settingKey}:{settingName}' setting is required to {requiredTo}");
}
