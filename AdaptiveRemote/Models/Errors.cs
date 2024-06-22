using System.Configuration;

namespace AdaptiveRemote.Models;

internal static class Errors
{
    public static Exception Telemetry_ConnectionStringRequired(string settingKey, string settingName)
        => new ConfigurationErrorsException($"The '{settingKey}:{settingName}' setting is required to publish telemetry");

    public static Exception Commands_UnsupportedCommandType(Command command, string argumentName)
        => new ArgumentException($"Cannot execute {command}", argumentName);

    internal static Exception TiVo_IPAddressRequired(string settingKey, string settingName)
        => new ConfigurationErrorsException($"The '{settingKey}:{settingName}' setting is required to connect to the TiVo");
    internal static Exception TiVo_CannotInterpretCommand(string commandId, string argName)
        => new ArgumentException($"Unable to interpret '{commandId}' as a TiVo command", argName);
    internal static Exception TiVo_NotInitialized(string command)
        => new InvalidOperationException($"Could not send '{command}' to the TiVo because the connection to the TiVo was not created.");
}
