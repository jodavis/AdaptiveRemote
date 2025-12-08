using System.Diagnostics;
using System.Text;

namespace AdaptiveRemote.EndToEndTests.Infrastructure;

/// <summary>
/// Manages a host process for end-to-end testing, capturing logs and monitoring lifecycle.
/// </summary>
public class HostProcess : IDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _outputLog = new();
    private readonly StringBuilder _errorLog = new();
    private readonly object _logLock = new();
    private bool _disposed;

    public string ExecutablePath { get; }
    public int ProcessId => _process.Id;
    public bool HasExited => _process.HasExited;
    public int ExitCode => _process.ExitCode;

    /// <summary>
    /// Gets all captured standard output.
    /// </summary>
    public string Output
    {
        get
        {
            lock (_logLock)
            {
                return _outputLog.ToString();
            }
        }
    }

    /// <summary>
    /// Gets all captured standard error.
    /// </summary>
    public string Error
    {
        get
        {
            lock (_logLock)
            {
                return _errorLog.ToString();
            }
        }
    }

    public HostProcess(string executablePath, string arguments)
    {
        ExecutablePath = executablePath;

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        _process.OutputDataReceived += OnOutputDataReceived;
        _process.ErrorDataReceived += OnErrorDataReceived;
    }

    /// <summary>
    /// Starts the host process.
    /// </summary>
    public void Start()
    {
        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    /// <summary>
    /// Waits for a specific log message to appear in the output.
    /// </summary>
    /// <param name="expectedMessage">The message to wait for.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <returns>True if the message was found, false if timeout occurred.</returns>
    public async Task<bool> WaitForLogMessageAsync(string expectedMessage, TimeSpan timeout)
    {
        DateTime startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            if (HasExited)
            {
                return false;
            }

            lock (_logLock)
            {
                if (_outputLog.ToString().Contains(expectedMessage, StringComparison.OrdinalIgnoreCase) ||
                    _errorLog.ToString().Contains(expectedMessage, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            await Task.Delay(100);
        }

        return false;
    }

    /// <summary>
    /// Checks if the log contains any error or warning messages.
    /// </summary>
    public bool HasErrorsOrWarnings()
    {
        lock (_logLock)
        {
            string fullLog = _outputLog.ToString() + _errorLog.ToString();
            // Check for common error/warning indicators
            return fullLog.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                   fullLog.Contains("Warning", StringComparison.OrdinalIgnoreCase) ||
                   fullLog.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                   fullLog.Contains("Failed", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Kills the process if it's still running.
    /// </summary>
    public void Kill()
    {
        if (!_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000); // Wait up to 5 seconds for clean exit
            }
            catch
            {
                // Process may have already exited
            }
        }
    }

    /// <summary>
    /// Waits for the process to exit.
    /// </summary>
    public async Task<bool> WaitForExitAsync(TimeSpan timeout)
    {
        return await Task.Run(() => _process.WaitForExit((int)timeout.TotalMilliseconds));
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            lock (_logLock)
            {
                _outputLog.AppendLine(e.Data);
            }
        }
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            lock (_logLock)
            {
                _errorLog.AppendLine(e.Data);
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Kill();
            _process.Dispose();
            _disposed = true;
        }
    }
}
