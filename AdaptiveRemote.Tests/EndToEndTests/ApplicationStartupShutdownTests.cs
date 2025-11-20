using AdaptiveRemote.Models;
using AdaptiveRemote.Services;
using AdaptiveRemote.Services.Lifecycle;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Windows.Threading;

namespace AdaptiveRemote.EndToEndTests;

/// <summary>
/// End-to-end tests for application startup and shutdown.
/// These tests verify that the application can start successfully, reach a ready state,
/// and shut down cleanly without errors.
/// </summary>
[TestClass]
public class ApplicationStartupShutdownTests
{
    private const int StartupTimeoutSeconds = 30;
    private const int ShutdownTimeoutSeconds = 30;

    public TestContext? TestContext { get; set; }

    /// <summary>
    /// Tests that the application can start up, reach ready state, and shut down cleanly
    /// with fake device services (no real TV connections).
    /// </summary>
    [TestMethod]
    [Timeout((StartupTimeoutSeconds + ShutdownTimeoutSeconds + 10) * 1000)] // Add buffer for test overhead
    public void ApplicationStartupAndShutdown_WithFakeServices_StartsAndShutsDownCleanly()
    {
        // This test must run on an STA thread for WPF
        Exception? testException = null;
        Thread staThread = new Thread(() =>
        {
            try
            {
                RunApplicationTestAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                testException = ex;
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        if (testException != null)
        {
            throw testException;
        }
    }

    private async Task RunApplicationTestAsync()
    {
        // Arrange
        ConcurrentBag<string> logs = new();
        TestLogCollector logCollector = new(logs, TestContext);
        LifecycleView? viewModel = null;
        IHost? host = null;
        Task? appLoopTask = null;
        Stopwatch stopwatch = Stopwatch.StartNew();
        
        try
        {
            TestContext?.WriteLine($"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Starting application startup test");

            // Create the ViewModel and Controller without WPF window
            viewModel = new LifecycleView();
            ILifecycleViewController controller = new LifecycleViewController(viewModel);
            
            TestContext?.WriteLine($"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Created ViewModel and Controller");

            // Build the host using the same configuration as the real application
            string[] args = Array.Empty<string>();
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .ConfigureAppSettings(args)
                .ConfigureApp()
                .ConfigureServices(services =>
                {
                    // Add the precreated services (without MainWindow for testing)
                    services.AddSingleton(controller);
                    services.AddSingleton(viewModel);
                    
                    // Add logging to capture output
                    services.AddLogging(logging =>
                    {
                        logging.AddProvider(new TestLogProvider(logCollector));
                    });
                });

            host = hostBuilder.Build();
            
            TestContext?.WriteLine($"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Built host");

            // Set up shutdown command that stops the host
            viewModel.ShutdownCommand = new ActionCommand(async () =>
            {
                TestContext?.WriteLine($"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Shutdown command invoked");
                await host.StopAsync();
            });
            
            // Act - Start the application host
            appLoopTask = host.RunAsync();
            
            TestContext?.WriteLine($"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Started application host");

            // Wait for the application to reach the Ready state
            bool reachedReady = await WaitForReadyStateAsync(
                viewModel, 
                TimeSpan.FromSeconds(StartupTimeoutSeconds),
                stopwatch);

            // Assert - Application reached ready state
            reachedReady.Should().BeTrue(
                "Application should reach Ready state within {0} seconds. Logs:\n{1}",
                StartupTimeoutSeconds,
                GetLogsSummary(logs));

            TestContext?.WriteLine($"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Application reached Ready state");

            // Check for fatal errors during startup
            viewModel.FatalError.Should().BeNull(
                "No fatal errors should occur during startup. Logs:\n{0}",
                GetLogsSummary(logs));

            // Act - Trigger shutdown
            TestContext?.WriteLine($"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Triggering shutdown");
            viewModel.ShutdownCommand?.Execute(null);

            // Wait for the application to complete
            bool completedCleanly = await WaitForApplicationExitAsync(
                appLoopTask, 
                TimeSpan.FromSeconds(ShutdownTimeoutSeconds),
                stopwatch);

            // Assert - Application shut down cleanly
            completedCleanly.Should().BeTrue(
                "Application should shut down within {0} seconds. Logs:\n{1}",
                ShutdownTimeoutSeconds,
                GetLogsSummary(logs));

            TestContext?.WriteLine($"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Application shut down successfully");

            // Verify no exceptions occurred
            if (appLoopTask.IsFaulted)
            {
                Assert.Fail("Application loop faulted during execution: {0}\n\nLogs:\n{1}",
                    appLoopTask.Exception,
                    GetLogsSummary(logs));
            }

            TestContext?.WriteLine($"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Test completed successfully");
        }
        catch (Exception ex)
        {
            // Collect diagnostic information
            TestContext?.WriteLine($"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Test failed with exception: {ex}");
            TestContext?.WriteLine("=== DIAGNOSTIC INFORMATION ===");
            TestContext?.WriteLine($"Application Loop Task Status: {appLoopTask?.Status}");
            TestContext?.WriteLine($"Current Lifecycle Phase: {viewModel?.CurrentPhase}");
            TestContext?.WriteLine($"Fatal Error: {viewModel?.FatalError}");
            TestContext?.WriteLine($"Task Name: {viewModel?.TaskName}");
            TestContext?.WriteLine("\n=== COLLECTED LOGS ===");
            TestContext?.WriteLine(GetLogsSummary(logs));
            throw;
        }
        finally
        {
            // Cleanup: Ensure the application is stopped
            if (host != null && appLoopTask != null && !appLoopTask.IsCompleted)
            {
                TestContext?.WriteLine($"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Force stopping host (cleanup)");
                try
                {
                    await Task.WhenAny(
                        host.StopAsync(TimeSpan.FromSeconds(5)),
                        Task.Delay(TimeSpan.FromSeconds(5)));
                }
                catch (Exception cleanupEx)
                {
                    TestContext?.WriteLine($"Exception during cleanup: {cleanupEx}");
                }
            }

            host?.Dispose();
            TestContext?.WriteLine($"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Test cleanup complete");
        }
    }

    /// <summary>
    /// Waits for the application to reach the Ready lifecycle phase.
    /// </summary>
    private async Task<bool> WaitForReadyStateAsync(
        LifecycleView viewModel, 
        TimeSpan timeout,
        Stopwatch stopwatch)
    {
        DateTime startTime = DateTime.UtcNow;
        
        while (DateTime.UtcNow - startTime < timeout)
        {
            if (viewModel.CurrentPhase == LifecyclePhase.Ready)
            {
                return true;
            }

            if (viewModel.CurrentPhase == LifecyclePhase.FatalError)
            {
                TestContext?.WriteLine(
                    $"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Application entered FatalError state: {viewModel.FatalError}");
                return false;
            }

            TestContext?.WriteLine(
                $"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Waiting for Ready state... Current: {viewModel.CurrentPhase}, Task: {viewModel.TaskName}");
            
            await Task.Delay(500);
        }

        TestContext?.WriteLine(
            $"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Timeout waiting for Ready state. Final state: {viewModel.CurrentPhase}");
        return false;
    }

    /// <summary>
    /// Waits for the application loop task to complete.
    /// </summary>
    private async Task<bool> WaitForApplicationExitAsync(
        Task appLoopTask, 
        TimeSpan timeout,
        Stopwatch stopwatch)
    {
        Task completedTask = await Task.WhenAny(appLoopTask, Task.Delay(timeout));
        
        if (completedTask == appLoopTask)
        {
            return true;
        }

        TestContext?.WriteLine(
            $"[{stopwatch.Elapsed:mm\\:ss\\.fff}] Timeout waiting for application exit. Task status: {appLoopTask.Status}");
        return false;
    }

    /// <summary>
    /// Gets a formatted summary of collected logs.
    /// </summary>
    private static string GetLogsSummary(ConcurrentBag<string> logs)
    {
        if (logs.IsEmpty)
        {
            return "(No logs collected)";
        }

        StringBuilder sb = new();
        foreach (string log in logs.OrderBy(l => l))
        {
            sb.AppendLine(log);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Collects logs from the application for diagnostic purposes.
    /// </summary>
    private class TestLogCollector
    {
        private readonly ConcurrentBag<string> _logs;
        private readonly TestContext? _testContext;

        public TestLogCollector(ConcurrentBag<string> logs, TestContext? testContext)
        {
            _logs = logs;
            _testContext = testContext;
        }

        public void Log(string message)
        {
            string timestampedMessage = $"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}";
            _logs.Add(timestampedMessage);
            _testContext?.WriteLine(timestampedMessage);
        }
    }

    /// <summary>
    /// Custom logging provider to capture application logs during testing.
    /// </summary>
    private class TestLogProvider : ILoggerProvider
    {
        private readonly TestLogCollector _collector;

        public TestLogProvider(TestLogCollector collector)
        {
            _collector = collector;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(categoryName, _collector);
        }

        public void Dispose()
        {
        }

        private class TestLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly TestLogCollector _collector;

            public TestLogger(string categoryName, TestLogCollector collector)
            {
                _categoryName = categoryName;
                _collector = collector;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                string message = $"[{logLevel}] {_categoryName}: {formatter(state, exception)}";
                if (exception != null)
                {
                    message += $"\nException: {exception}";
                }
                _collector.Log(message);
            }
        }
    }
}
