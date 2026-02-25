using Microsoft.Extensions.Configuration;

namespace AdaptiveRemote.Services.ProgrammaticSettings;

/// <summary>
/// An <see cref="IConfigurationSource"/> that reads a flat <c>key=value</c> file
/// where keys may contain <c>:</c> as a hierarchy separator (e.g. <c>IRData:Power=...</c>).
/// Lines beginning with <c>#</c> are treated as comments and skipped.
/// </summary>
internal sealed class ProgrammaticSettingsConfigurationSource : IConfigurationSource
{
    private readonly string _path;
    private readonly bool _optional;

    public ProgrammaticSettingsConfigurationSource(string path, bool optional = true)
    {
        _path = path;
        _optional = optional;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
        => new ProgrammaticSettingsConfigurationProvider(_path, _optional);
}

internal sealed class ProgrammaticSettingsConfigurationProvider : ConfigurationProvider
{
    private readonly string _path;
    private readonly bool _optional;

    public ProgrammaticSettingsConfigurationProvider(string path, bool optional)
    {
        _path = path;
        _optional = optional;
    }

    public override void Load()
    {
        if (!File.Exists(_path))
        {
            if (!_optional)
            {
                throw new FileNotFoundException($"Programmatic settings file not found: {_path}");
            }

            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (string line in File.ReadAllLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            int separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            string key = line[..separatorIndex].Trim();
            string value = line[(separatorIndex + 1)..].Trim();

            if (!string.IsNullOrEmpty(key))
            {
                Data[key] = value;
            }
        }
    }
}
