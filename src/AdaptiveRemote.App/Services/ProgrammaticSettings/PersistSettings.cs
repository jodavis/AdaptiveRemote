using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AdaptiveRemote.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Threading;

namespace AdaptiveRemote.Services.ProgrammaticSettings;

internal class PersistSettings : IPersistSettings
{
    private const string Separator = "=";
    private const string NameKey = "name";
    private const string NamePattern = @"\w+(:\w+)*";
    private const string ValueKey = "value";
    private const string ValuePattern = @"[^\r\n]*";
    private const string SectionPattern = @"\w+";

    private static readonly Regex KeyRegex = new($"^{NamePattern}$", RegexOptions.Singleline);
    private static readonly Regex ValueRegex = new($"^{ValuePattern}$", RegexOptions.Singleline);
    private static readonly Regex LineRegex = new($"^(?<{NameKey}>{NamePattern}){Separator}(?<{ValueKey}>{ValuePattern})$");
    private static readonly Regex SectionHeaderRegex = new($@"^\[(?<section>{SectionPattern})\]$", RegexOptions.Singleline);

    private readonly IFileSystem _fileSystem;
    private readonly string _filePath;
    private readonly MessageLogger _logger;

    private readonly AsyncLazy<ConcurrentDictionary<string, string>> _lazyValues;
    private readonly object _lockObject = new();

    private bool _needsSave = false;
    private bool _isSaving = false;

    public PersistSettings(IFileSystem fileSystem, IOptions<ProgrammaticSettings> settings, ILogger<PersistSettings> logger)
    {
        _fileSystem = fileSystem;
        _filePath = settings.Value.ProgrammaticSettingsPath;
        _logger = new(logger);

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
            _logger.ProgrammaticSettings_Rejected(name, value, error.Message);
            throw;
        }
    }

    private async Task SetAsync(string name, string value)
    {
        try
        {
            ConcurrentDictionary<string, string> values = await _lazyValues.GetValueAsync();

            if (values.TryGetValue(name, out string? oldValue))
            {
                _logger.ProgrammaticSettings_ReplaceSetting(name, oldValue, value);
            }
            else
            {
                _logger.ProgrammaticSettings_AddSetting(name, value);
            }
            values[name] = value;

            await SaveSettingsAsync(values);
        }
        catch (Exception error)
        {
            _logger.ProgrammaticSettings_Error(name, value, error);
        }
    }

    private async Task<ConcurrentDictionary<string, string>> LoadExistingSettingsAsync()
    {
        ConcurrentDictionary<string, string> values = new();

        if (_fileSystem.FileExists(_filePath))
        {
            _logger.ProgrammaticSettings_LoadingExistingSettings(_filePath);

            using Stream readStream = _fileSystem.OpenRead(_filePath);
            using StreamReader reader = new(readStream);

            string? currentSection = null;
            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                string trimmed = line.Trim();

                Match sectionMatch = SectionHeaderRegex.Match(trimmed);
                if (sectionMatch.Success)
                {
                    currentSection = sectionMatch.Groups["section"].Value;
                    continue;
                }

                Match lineMatch = LineRegex.Match(trimmed);
                if (lineMatch.Success)
                {
                    string key = lineMatch.Groups[NameKey].Value;
                    string value = lineMatch.Groups[ValueKey].Value;
                    string fullKey = currentSection is not null ? $"{currentSection}:{key}" : key;
                    values.TryAdd(fullKey, value);
                }
            }

            _logger.ProgrammaticSettings_LoadedExistingSettings(values.Count, _filePath);
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
                    _logger.ProgrammaticSettings_SavedSettings(_filePath);

                    _isSaving = false;
                    return;
                }
                _needsSave = false;
            }

            await DoSaveAsync(values);
        }

        async Task DoSaveAsync(ConcurrentDictionary<string, string> values)
        {
            _logger.ProgrammaticSettings_SavingSettings(values.Count, _filePath);

            _fileSystem.CreateDirectory(Path.GetDirectoryName(_filePath)!, recursive: true);

            using Stream writeStream = _fileSystem.OpenWrite(_filePath);
            using StreamWriter writer = new(writeStream);

            // Write keys with no section first
            foreach (KeyValuePair<string, string> pair in values.Where(p => !p.Key.Contains(':')))
            {
                await writer.WriteLineAsync($"{pair.Key}{Separator}{pair.Value}");
            }

            // Group remaining keys by section (part before first ':')
            foreach (IGrouping<string, KeyValuePair<string, string>> group in values
                .Where(p => p.Key.Contains(':'))
                .GroupBy(p => p.Key[..p.Key.IndexOf(':')]))
            {
                await writer.WriteLineAsync($"[{group.Key}]");
                foreach (KeyValuePair<string, string> pair in group)
                {
                    int colon = pair.Key.IndexOf(':');
                    await writer.WriteLineAsync($"{pair.Key[(colon + 1)..]}{Separator}{pair.Value}");
                }
            }

            await writer.FlushAsync();
        }
    }
}
