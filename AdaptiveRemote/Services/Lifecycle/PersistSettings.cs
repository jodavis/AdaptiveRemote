using System.IO;
using System.Text.RegularExpressions;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Services.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Lifecycle;

internal class PersistSettings : IPersistSettings
{
    private const string Separator = "=";
    private const string NameKey = "name";
    private const string NamePattern = @"[\w:]+";
    private const string ValueKey = "value";
    private const string ValuePattern = @".*";

    private static Regex KeyRegex = new($"^{NamePattern}$");
    private static Regex ValueRegex = new($"^{ValuePattern}$");
    private static Regex LineRegex = new($"^(?<{NameKey}>{NamePattern}){Separator}(?<{ValueKey}>{ValuePattern})$");

    private readonly IFileSystem _fileSystem;
    private readonly string _filePath;
    private readonly ILogger<PersistSettings> _logger;

    public PersistSettings(IFileSystem fileSystem, IOptions<HostSettings> settings, ILogger<PersistSettings> logger)
    {
        _fileSystem = fileSystem;
        _filePath = settings.Value.ProgrammaticSettingsPath;
        _logger = logger;
    }

    void IPersistSettings.Set(string name, string value)
    {
        // TODO: Input validation
        // TODO: Error handling/logging

        _ = SetAsync(name, value);
    }

    private async Task SetAsync(string name, string value)
    {
        Dictionary<string, string> values = await LoadExistingSettingsAsync();

        // TODO: Different message when replacing
        _logger.LogInformation(Message.ProgrammaticSettings_AddSetting, name, value);
        values[name] = value;

        await SaveSettingsAsync(values);
    }

    private async Task<Dictionary<string, string>> LoadExistingSettingsAsync()
    {
        _logger.LogInformation(Message.ProgrammaticSettings_LoadingExistingSettings, _filePath);
        Dictionary<string, string> values = new();

        using Stream readStream = _fileSystem.OpenRead(_filePath);
        using StreamReader reader = new(readStream);

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            Match match = LineRegex.Match(line);
            values.Add(match.Groups[NameKey].Value, match.Groups[ValueKey].Value);
        }

        _logger.LogInformation(Message.ProgrammaticSettings_LoadedExistingSettings, values.Count, _filePath);
        return values;
    }

    private async Task SaveSettingsAsync(Dictionary<string, string> values)
    {
        // TODO: Wait while already saving
        _logger.LogInformation(Message.ProgrammaticSettings_SavingSettings, values.Count, _filePath);
        using Stream writeStream = _fileSystem.OpenWrite(_filePath);
        using StreamWriter writer = new(writeStream);

        foreach (KeyValuePair<string, string> pair in values)
        {
            await writer.WriteLineAsync($"{pair.Key}{Separator}{pair.Value}");
        }
        await writer.FlushAsync();

        _logger.LogInformation(Message.ProgrammaticSettings_SavedSettings, _filePath);
    }
}
