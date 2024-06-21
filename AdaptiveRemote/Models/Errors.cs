using System.Configuration;

namespace AdaptiveRemote.Models;

internal static class Errors
{
    public static Exception Telemetry_ConnectionStringRequired(string settingKey, string settingName)
        => new ConfigurationErrorsException($"The '{settingKey}:{settingName}' setting is required to publish telemetry");
    public static Exception Commands_UnsupportedCommandType(Command command, string argumentName)
        => new ArgumentException($"Cannot execute {command}", argumentName);
}
