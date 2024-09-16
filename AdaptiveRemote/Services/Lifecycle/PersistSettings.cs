using System.IO;
using System.Text.RegularExpressions;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using AdaptiveRemote.Services.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Lifecycle;

internal class PersistSettings : IPersistSettings
{
    private const string Separator = "=";
    private const string NameKey = "name";
    private const string NamePattern = @"\w+(:\w+)*";
    private const string ValueKey = "value";
    private const string ValuePattern = @"[^\\r\\n]*";

    private static Regex KeyRegex = new($"^{NamePattern}$", RegexOptions.Singleline);
    private static Regex ValueRegex = new($"^{ValuePattern}$", RegexOptions.Singleline);
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
        ValidateInputs(name, value);

        _ = SetAsync(name, value);
    }

    private void ValidateInputs(string name, string value)
    {
        try
        {
            if (!KeyRegex.IsMatch(name) || name.Contains('\n'))
            {
                throw Errors.PersistSettings_InvalidName(nameof(name), name);
            }
            if (!ValueRegex.IsMatch(value))
            {
                throw Errors.PersistSettings_InvalidValue(nameof(value), value);
            }
        }
        catch (ArgumentException error)
        {
            _logger.LogError(Message.ProgrammaticSettings_Rejected, name, value, error.Message);
            throw;
        }
    }

    private async Task SetAsync(string name, string value)
    {
        Dictionary<string, string> values;
        try
        {
            values = await LoadExistingSettingsAsync();

            if (values.TryGetValue(name, out string? oldValue))
            {
                _logger.LogInformation(Message.ProgrammaticSettings_ReplaceSetting, name, oldValue, name, value);
            }
            else
            {
                _logger.LogInformation(Message.ProgrammaticSettings_AddSetting, name, value);
            }
            values[name] = value;

            await SaveSettingsAsync(values);
        }
        catch (Exception error)
        {
            _logger.LogError(Message.ProgrammaticSettings_Error, name, value, error);
        }
    }

    private async Task<Dictionary<string, string>> LoadExistingSettingsAsync()
    {
        Dictionary<string, string> values = new();

        if (_fileSystem.FileExists(_filePath))
        {
            _logger.LogInformation(Message.ProgrammaticSettings_LoadingExistingSettings, _filePath);

            using Stream readStream = _fileSystem.OpenRead(_filePath);
            using StreamReader reader = new(readStream);

            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                Match match = LineRegex.Match(line);
                values.Add(match.Groups[NameKey].Value, match.Groups[ValueKey].Value);
            }

            _logger.LogInformation(Message.ProgrammaticSettings_LoadedExistingSettings, values.Count, _filePath);
        }

        return values;
    }

    private async Task SaveSettingsAsync(Dictionary<string, string> values)
    {
        // TODO: Wait while already saving
        _logger.LogInformation(Message.ProgrammaticSettings_SavingSettings, values.Count, _filePath);

        EnsurePathFor(_filePath);

        using Stream writeStream = _fileSystem.OpenWrite(_filePath);
        using StreamWriter writer = new(writeStream);

        foreach (KeyValuePair<string, string> pair in values)
        {
            await writer.WriteLineAsync($"{pair.Key}{Separator}{pair.Value}");
        }
        await writer.FlushAsync();

        _logger.LogInformation(Message.ProgrammaticSettings_SavedSettings, _filePath);

        void EnsurePathFor(string filePath)
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (directory is not null &&
                !_fileSystem.DirectoryExists(directory))
            {
                EnsurePathFor(directory);
                _fileSystem.CreateDirectory(directory);
            }
        }
    }
}
