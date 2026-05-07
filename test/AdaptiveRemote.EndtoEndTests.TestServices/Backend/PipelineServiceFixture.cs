using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using AdaptiveRemote.EndToEndTests.TestServices.Backend;

namespace AdaptiveRemote.EndToEndTests.TestServices.Backend;

/// <summary>
/// Manages the lifecycle of both RawLayoutService and LayoutProcessingService for
/// end-to-end pipeline integration tests. Both services share a single LocalStack
/// instance (via <see cref="ServiceFixture"/>). A <see cref="StubCompiledLayoutService"/>
/// runs in-process to accept compiled layout saves without requiring a real
/// CompiledLayoutService process.
///
/// The LayoutProcessingService orchestrator is enabled (unlike the single-service
/// health-check fixture) so the full SQS polling pipeline executes.
/// </summary>
public sealed class PipelineServiceFixture : IDisposable
{
    // LocalStack is shared across all scenarios.
    private static LocalStackFixture? _sharedLocalStack;
    private static readonly SemaphoreSlim _localStackInitLock = new(1, 1);

    private Process? _rawLayoutProcess;
    private Process? _processingProcess;
    private readonly StringBuilder _rawLayoutLogs = new();
    private readonly StringBuilder _processingLogs = new();
    private readonly object _logLock = new();
    private TestJwtAuthority? _jwtAuthority;
    private StubCompiledLayoutService? _stubCompiledLayoutService;

    private readonly string _rawLayoutServiceUrl;
    private readonly string _processingServiceUrl;

    // Use a unique user ID per fixture instance for data isolation.
    private readonly string _testUserId = $"test-user-{Guid.NewGuid():N}";

    public string RawLayoutServiceUrl => _rawLayoutServiceUrl;
    public string ProcessingServiceUrl => _processingServiceUrl;

    /// <summary>HttpClient targeting RawLayoutService with a valid bearer token.</summary>
    public HttpClient RawLayoutHttpClient { get; private set; } = null!;

    /// <summary>HttpClient targeting LayoutProcessingService with a valid bearer token.</summary>
    public HttpClient ProcessingHttpClient { get; private set; } = null!;

    public PipelineServiceFixture()
    {
        _rawLayoutServiceUrl = $"http://localhost:{GetFreePort()}";
        _processingServiceUrl = $"http://localhost:{GetFreePort()}";
    }

    public async Task StartAsync(bool forceValidationInvalid = false)
    {
        LocalStackFixture localStack = await GetSharedLocalStackAsync();
        await localStack.CreateTableAsync("RawLayouts");
        await localStack.CreateSqsQueueAsync("LayoutProcessingQueue");

        _jwtAuthority = new TestJwtAuthority();

        // Start the in-process stub for CompiledLayoutService.
        _stubCompiledLayoutService = new StubCompiledLayoutService();
        await _stubCompiledLayoutService.StartAsync();

        string repoRoot = FindRepoRoot()
            ?? throw new InvalidOperationException("Could not find repository root (no .git directory found)");

        // Generate a service account token for LayoutProcessingService → RawLayoutService calls.
        // This is a long-lived token representing the processing service identity.
        string serviceAccountUserId = "service-account-layout-processor";
        string serviceAccountToken = _jwtAuthority.CreateToken(serviceAccountUserId);

        // Start RawLayoutService
        _rawLayoutProcess = StartServiceProcess(
            repoRoot,
            "AdaptiveRemote.Backend.RawLayoutService",
            _rawLayoutServiceUrl,
            new Dictionary<string, string>
            {
                ["AWS_ACCESS_KEY_ID"] = "test",
                ["AWS_SECRET_ACCESS_KEY"] = "test",
                ["DynamoDB__ServiceUrl"] = localStack.ServiceUrl,
                ["DynamoDB__Region"] = localStack.Region,
                ["DynamoDB__TableName"] = "RawLayouts",
                ["Sqs__ServiceUrl"] = localStack.ServiceUrl,
                ["Sqs__QueueUrl"] = localStack.GetSqsQueueUrl("LayoutProcessingQueue"),
                ["Sqs__Region"] = localStack.Region,
                ["Cognito__Authority"] = _jwtAuthority.Authority,
                ["LocalStack__BaseUrl"] = _jwtAuthority.Authority,
            },
            _rawLayoutLogs,
            _logLock);

        // Start LayoutProcessingService with orchestrator enabled, pointing at:
        //   - LocalStack SQS for the queue
        //   - The in-process RawLayoutService (with a service account token for auth)
        //   - The in-process stub CompiledLayoutService
        Dictionary<string, string> processingEnv = new()
        {
            ["AWS_ACCESS_KEY_ID"] = "test",
            ["AWS_SECRET_ACCESS_KEY"] = "test",
            ["Sqs__ServiceUrl"] = localStack.ServiceUrl,
            ["Sqs__QueueUrl"] = localStack.GetSqsQueueUrl("LayoutProcessingQueue"),
            ["Sqs__Region"] = localStack.Region,
            ["RawLayoutService__BaseUrl"] = _rawLayoutServiceUrl,
            ["RawLayoutService__ServiceAccountToken"] = serviceAccountToken,
            ["CompiledLayoutService__BaseUrl"] = _stubCompiledLayoutService.ServiceUrl,
            ["Cognito__Authority"] = _jwtAuthority.Authority,
            ["LocalStack__BaseUrl"] = _jwtAuthority.Authority,
            // Enable the orchestrator for pipeline tests
            ["Orchestrator__Enabled"] = "true",
        };

        if (forceValidationInvalid)
        {
            processingEnv["Validation__ForceInvalid"] = "true";
        }

        _processingProcess = StartServiceProcess(
            repoRoot,
            "AdaptiveRemote.Backend.LayoutProcessingService",
            _processingServiceUrl,
            processingEnv,
            _processingLogs,
            _logLock);

        // Wait for both services to be healthy.
        await WaitForHealthAsync(_rawLayoutServiceUrl, "RawLayoutService");
        await WaitForHealthAsync(_processingServiceUrl, "LayoutProcessingService");

        string token = _jwtAuthority.CreateToken(_testUserId);
        RawLayoutHttpClient = CreateBearerHttpClient(_rawLayoutServiceUrl, token);
        ProcessingHttpClient = CreateBearerHttpClient(_processingServiceUrl, token);
    }

    public string CreateToken(string? sub = null)
    {
        if (_jwtAuthority is null)
        {
            throw new InvalidOperationException("StartAsync() must be called before CreateToken()");
        }

        return _jwtAuthority.CreateToken(sub ?? _testUserId);
    }

    public string GetRawLayoutLogs()
    {
        lock (_logLock)
        {
            return _rawLayoutLogs.ToString();
        }
    }

    public string GetProcessingLogs()
    {
        lock (_logLock)
        {
            return _processingLogs.ToString();
        }
    }

    /// <summary>
    /// Polls the processing service logs until the given text appears or the timeout elapses.
    /// </summary>
    public async Task<bool> WaitForLogAsync(string text, TimeSpan? timeout = null)
    {
        TimeSpan deadline = timeout ?? TimeSpan.FromSeconds(30);
        DateTime cutoff = DateTime.UtcNow + deadline;

        while (DateTime.UtcNow < cutoff)
        {
            string logs = GetProcessingLogs();
            if (logs.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            await Task.Delay(500);
        }

        return false;
    }

    public void Dispose()
    {
        KillProcess(_rawLayoutProcess);
        KillProcess(_processingProcess);
        _stubCompiledLayoutService?.Dispose();
        RawLayoutHttpClient?.Dispose();
        ProcessingHttpClient?.Dispose();
        _jwtAuthority?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Process StartServiceProcess(
        string repoRoot,
        string serviceName,
        string serviceUrl,
        Dictionary<string, string> env,
        StringBuilder logBuffer,
        object logLock)
    {
        string projectPath = Path.Combine(repoRoot, "src", serviceName, $"{serviceName}.csproj");

        if (!File.Exists(projectPath))
        {
            throw new InvalidOperationException($"Project file not found: {projectPath}");
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" --no-build --no-launch-profile",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_URLS"] = serviceUrl;

        foreach (KeyValuePair<string, string> kvp in env)
        {
            startInfo.Environment[kvp.Key] = kvp.Value;
        }

        Process process = new() { StartInfo = startInfo };

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                lock (logLock)
                {
                    logBuffer.AppendLine(args.Data);
                }
            }
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                lock (logLock)
                {
                    logBuffer.AppendLine($"ERROR: {args.Data}");
                }
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private static async Task WaitForHealthAsync(string serviceUrl, string serviceName)
    {
        using HttpClient client = new()
        {
            BaseAddress = new Uri(serviceUrl),
            Timeout = TimeSpan.FromSeconds(5),
        };

        for (int i = 0; i < 30; i++)
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync("/health");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Ignore connection errors during startup
            }

            await Task.Delay(1000);
        }

        throw new InvalidOperationException($"{serviceName} did not become healthy within 30 seconds (polling {serviceUrl}/health)");
    }

    private static HttpClient CreateBearerHttpClient(string baseUrl, string token)
        => new(new BearerTokenHandler(token)) { BaseAddress = new Uri(baseUrl) };

    private static void KillProcess(Process? process)
    {
        if (process is null || process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch
        {
            // Best effort
        }
        finally
        {
            process.Dispose();
        }
    }

    private static string? FindRepoRoot()
    {
        string? dir = Directory.GetCurrentDirectory();
        while (dir is not null && !Directory.Exists(Path.Combine(dir, ".git")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        return dir;
    }

    private static async Task<LocalStackFixture> GetSharedLocalStackAsync()
    {
        await _localStackInitLock.WaitAsync();
        try
        {
            if (_sharedLocalStack is null)
            {
                LocalStackFixture fixture = new();
                await fixture.StartAsync();
                _sharedLocalStack = fixture;
            }

            return _sharedLocalStack;
        }
        finally
        {
            _localStackInitLock.Release();
        }
    }

    private static int GetFreePort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class BearerTokenHandler : DelegatingHandler
    {
        private readonly string _token;

        public BearerTokenHandler(string token)
            : base(new HttpClientHandler())
        {
            _token = token;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
