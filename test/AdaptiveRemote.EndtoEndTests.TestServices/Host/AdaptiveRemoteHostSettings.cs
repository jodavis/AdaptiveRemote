using System.Collections.Immutable;

namespace AdaptiveRemote.EndtoEndTests.Host;

public enum UIServiceType
{
    Playwright,
    BlazorWebView
}

public record AdaptiveRemoteHostSettings(
    UIServiceType UIService,
    string ExePath,
    string CommandLineArgs,
    string WorkingDirectory,
    ImmutableDictionary<string, string> EnvironmentVariables,
    TimeSpan StartupTimeout,
    TimeSpan RpcTimeout,
    TimeSpan ShutdownTimeout)
{
    public AdaptiveRemoteHostSettings(
        UIServiceType UIService,
        string ExePath,
        string CommandLineArgs = "",
        string? WorkingDirectory = null,
        ImmutableDictionary<string, string>? EnvironmentVariables = null)
        : this(UIService, ExePath, CommandLineArgs,
               WorkingDirectory: WorkingDirectory ?? Path.GetDirectoryName(ExePath) ?? AppContext.BaseDirectory,
               EnvironmentVariables: EnvironmentVariables ?? ImmutableDictionary<string, string>.Empty,
               StartupTimeout: TimeSpan.FromSeconds(120),
               RpcTimeout: TimeSpan.FromSeconds(30),
               ShutdownTimeout: TimeSpan.FromSeconds(30))
    { }

    public AdaptiveRemoteHostSettings AddCommandLineArgs(string args)
        => this with
        {
            CommandLineArgs = CommandLineArgs.Length > 0
                ? $"{CommandLineArgs} {args}"
                : args
        };
}
