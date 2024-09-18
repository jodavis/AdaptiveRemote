using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.ProgrammaticSettings;

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

    private readonly Lazy<Task<ConcurrentDictionary<string, string>>> _lazyValues;

    private bool _needsSave = false;
    private bool _isSaving = false;
    private object _lockObject = new();

    public PersistSettings(IFileSystem fileSystem, IOptions<ProgrammaticSettings> settings, ILogger<PersistSettings> logger)
    {
        _fileSystem = fileSystem;
        _filePath = settings.Value.ProgrammaticSettingsPath;
        _logger = logger;

        _lazyValues = new(LoadExistingSettingsAsync);
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
        try
        {
            ConcurrentDictionary<string, string> values = await _lazyValues.Value;

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

    private async Task<ConcurrentDictionary<string, string>> LoadExistingSettingsAsync()
    {
        ConcurrentDictionary<string, string> values = new();

        if (_fileSystem.FileExists(_filePath))
        {
            _logger.LogInformation(Message.ProgrammaticSettings_LoadingExistingSettings, _filePath);

            using Stream readStream = _fileSystem.OpenRead(_filePath);
            using StreamReader reader = new(readStream);

            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                Match match = LineRegex.Match(line);
                values.TryAdd(match.Groups[NameKey].Value, match.Groups[ValueKey].Value);
            }

            _logger.LogInformation(Message.ProgrammaticSettings_LoadedExistingSettings, values.Count, _filePath);
        }

        return values;
    }

    private async Task SaveSettingsAsync(ConcurrentDictionary<string, string> values)
    {
        lock (_lockObject)
        {
            _needsSave = true;
            if (_isSaving)
            {
                return;
            }
            _isSaving = true;
        }

        while (true)
        {
            lock (_lockObject)
            {
                if (!_needsSave)
                {
                    _logger.LogInformation(Message.ProgrammaticSettings_SavedSettings, _filePath);

                    _isSaving = false;
                    return;
                }
                _needsSave = false;
            }

            await DoSaveAsync(values);
        }

        async Task DoSaveAsync(ConcurrentDictionary<string, string> values)
        {
            _logger.LogInformation(Message.ProgrammaticSettings_SavingSettings, values.Count, _filePath);

            _fileSystem.CreateDirectory(Path.GetDirectoryName(_filePath)!, recursive: true);

            using Stream writeStream = _fileSystem.OpenWrite(_filePath);
            using StreamWriter writer = new(writeStream);

            foreach (KeyValuePair<string, string> pair in values)
            {
                await writer.WriteLineAsync($"{pair.Key}{Separator}{pair.Value}");
            }
            await writer.FlushAsync();
        }
    }
}
