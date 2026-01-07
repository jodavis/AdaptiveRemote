#pragma warning disable IDE0005 // False positive: GetService<T>() extension method requires Microsoft.Extensions.DependencyInjection
using AdaptiveRemote.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#pragma warning restore IDE0005

namespace AdaptiveRemote.Services.Conversation;

internal class SamplesRecorder : IHostedService
{
    private readonly ConversationSettings _settings;
    private readonly ISpeechRecognitionEngine _engine;
    private readonly ILogger<SamplesRecorder> _logger;
    private List<string> _logLines = new();

    public SamplesRecorder(ISpeechRecognitionEngine engine, IOptions<ConversationSettings> settings, ILogger<SamplesRecorder> logger)
    {
        _settings = settings.Value;
        _engine = engine;
        _logger = logger;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        if (_settings.RecordSamples)
        {
            _engine.SpeechRecognized += OnSpeechRecognized;
            _engine.SpeechRejected += OnSpeechRejected;
        }

        _logger.LogInformation("Started listening for speech samples");

        return Task.CompletedTask;
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        _engine.SpeechRecognized -= OnSpeechRecognized;
        _engine.SpeechRejected -= OnSpeechRejected;

        _logger.LogInformation("Stopped listening for speech samples");

        return Task.CompletedTask;
    }

    private void OnSpeechRecognized(object? sender, RecognizedSpeechEventArgs e)
    {
        RecordSpeechSample(e.Result);
    }

    private void OnSpeechRejected(object? sender, RecognizedSpeechEventArgs e)
    {
        RecordSpeechSample(e.Result, suffix: "_Rejected");
    }

    private void RecordSpeechSample(IRecognizedSpeech result, string suffix = "")
    {
        string outputPath = _settings.RecordingOutputPath ?? ".";

        string text = result.Text.ToPascalCase();
        string userName = _settings.RecordingUserName ?? "User";
        string fileName = $"{userName}_{text}{suffix}_{DateTime.Now:hhmmss}";
        string wavPath = $"{outputPath}\\{fileName}.wav";
        using (Stream waveStream = File.OpenWrite(wavPath))
        {
            result.WriteToWaveStream(waveStream);
            waveStream.Flush();
        }

        _logger.LogInformation("Saved {WaveFileName}", wavPath);

        string logPath = $"{outputPath}\\{fileName}_log.txt";
        using (Stream logStream = File.OpenWrite(logPath))
        {
            StreamWriter writer = new StreamWriter(logStream);
            List<string> lines = Interlocked.Exchange(ref _logLines, new());

            foreach (string line in lines)
            {
                writer.WriteLine(line);
            }
            writer.Flush();
        }

        _logger.LogInformation("Saved {LogFileName}", logPath);
    }

    internal class LoggerProvider : ILoggerProvider, ILogger
    {
        private readonly Lazy<SamplesRecorder> _samplesRecorder;

        public LoggerProvider(IServiceProvider serviceProvider)
        {
            _samplesRecorder = new(() => serviceProvider.GetService<IEnumerable<IHostedService>>()?.OfType<SamplesRecorder>().FirstOrDefault()
            ?? throw new Exception("Could not find SamplesRecorder in the service provider"));
        }

        ILogger ILoggerProvider.CreateLogger(string categoryName)
        {
            if (categoryName == "System.Speech.Recognition.SpeechRecognitionEngine")
            {
                return this;
            }
            return NullLogger.Instance;
        }

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _samplesRecorder.Value.AddLogLine($"[{DateTime.Now.ToString("hh:mm:ss")}] {state}");
        }

        bool ILogger.IsEnabled(LogLevel logLevel) => true;
        IDisposable? ILogger.BeginScope<TState>(TState state) => default!;

        void IDisposable.Dispose() { }
    }

    private void AddLogLine(string logLine)
    {
        _logLines.Add(logLine);
    }

    private class NullLogger : ILogger
    {
        public static ILogger Instance { get; } = new NullLogger();

        private NullLogger() { }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        public bool IsEnabled(LogLevel logLevel) => false;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default!;
    }
}
