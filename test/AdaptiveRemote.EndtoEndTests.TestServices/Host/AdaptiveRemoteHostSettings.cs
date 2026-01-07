using System.Collections.Immutable;

namespace AdaptiveRemote.EndtoEndTests.Host;

public enum UIServiceType
{
    Headless,
    BlazorWebView
}

public record AdaptiveRemoteHostSettings(
    string ExePath,
    string CommandLineArgs,
    string WorkingDirectory,
    ImmutableDictionary<string, string> EnvironmentVariables,
    TimeSpan StartupTimeout,
    TimeSpan RpcTimeout,
    TimeSpan ShutdownTimeout,
    UIServiceType UIService)
{
    public AdaptiveRemoteHostSettings(
        string ExePath,
        string CommandLineArgs = "",
        string? WorkingDirectory = null,
        ImmutableDictionary<string, string>? EnvironmentVariables = null,
        UIServiceType? UIService = null)
        : this(ExePath, CommandLineArgs,
              WorkingDirectory ?? Path.GetDirectoryName(ExePath) ?? AppContext.BaseDirectory,
              EnvironmentVariables ?? ImmutableDictionary<string, string>.Empty,
              StartupTimeout: TimeSpan.FromSeconds(120),
              RpcTimeout: TimeSpan.FromSeconds(30),
              ShutdownTimeout: TimeSpan.FromSeconds(30),
              UIService: UIService ?? (ExePath.Contains("Headless", StringComparison.OrdinalIgnoreCase) ? UIServiceType.Headless : UIServiceType.BlazorWebView))
    { }

    public AdaptiveRemoteHostSettings AddCommandLineArgs(string args)
        => this with
        {
            CommandLineArgs = CommandLineArgs.Length > 0
                ? $"{CommandLineArgs} {args}"
                : args
        };
}
