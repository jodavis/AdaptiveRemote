using System.Text.Json;
using AdaptiveRemote.Backend.Common.Logging;
using AdaptiveRemote.Backend.LayoutProcessingService.Configuration;
using AdaptiveRemote.Backend.LayoutProcessingService.Endpoints;
using AdaptiveRemote.Backend.LayoutProcessingService.Services;
using AdaptiveRemote.Contracts;
using Amazon.SQS;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Scalar.AspNetCore;

string? logFilePath = null;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--logFile")
    {
        logFilePath = args[i + 1];
        break;
    }
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

if (!string.IsNullOrEmpty(logFilePath))
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddFile(logFilePath);
}

// Configure SQS settings
SqsSettings sqsSettings = builder.Configuration
    .GetSection("Sqs")
    .Get<SqsSettings>() ?? new SqsSettings();

builder.Services.Configure<SqsSettings>(builder.Configuration.GetSection("Sqs"));

// Create SQS client
IAmazonSQS sqsClient;

if (!string.IsNullOrEmpty(sqsSettings.ServiceUrl))
{
    // LocalStack or custom endpoint
    AmazonSQSConfig sqsConfig = new()
    {
        ServiceURL = sqsSettings.ServiceUrl,
        AuthenticationRegion = sqsSettings.Region
    };

    string? accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
    string? secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

    if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
    {
        sqsClient = new AmazonSQSClient(
            new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey),
            sqsConfig);
    }
    else
    {
        sqsClient = new AmazonSQSClient(sqsConfig);
    }
}
else
{
    // Production AWS — use default credential chain (IAM roles, etc.)
    AmazonSQSConfig sqsConfig = new()
    {
        RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(sqsSettings.Region)
    };
    sqsClient = new AmazonSQSClient(sqsConfig);
}

builder.Services.AddSingleton(sqsClient);

// Configure HTTP client for RawLayoutService
RawLayoutServiceSettings rawLayoutSettings = builder.Configuration
    .GetSection("RawLayoutService")
    .Get<RawLayoutServiceSettings>() ?? new RawLayoutServiceSettings();

builder.Services.Configure<RawLayoutServiceSettings>(builder.Configuration.GetSection("RawLayoutService"));

// If a service account token is configured, attach it as a bearer token on outgoing requests.
// In production this will be replaced by IAM-signed requests or a Cognito M2M token.
bool hasServiceAccountToken = !string.IsNullOrEmpty(rawLayoutSettings.ServiceAccountToken);
if (hasServiceAccountToken)
{
    builder.Services.AddTransient(_ =>
        new ServiceAccountTokenHandler(rawLayoutSettings.ServiceAccountToken!));
}

void ConfigureRawLayoutClient(HttpClient client)
{
    if (!string.IsNullOrEmpty(rawLayoutSettings.BaseUrl))
    {
        client.BaseAddress = new Uri(rawLayoutSettings.BaseUrl);
    }
}

IHttpClientBuilder rawLayoutRepoBuilder =
    builder.Services.AddHttpClient<IRawLayoutRepository, HttpRawLayoutRepository>(ConfigureRawLayoutClient);
IHttpClientBuilder rawLayoutWriterBuilder =
    builder.Services.AddHttpClient<IRawLayoutStatusWriter, HttpRawLayoutStatusWriter>(ConfigureRawLayoutClient);

if (hasServiceAccountToken)
{
    rawLayoutRepoBuilder.AddHttpMessageHandler<ServiceAccountTokenHandler>();
    rawLayoutWriterBuilder.AddHttpMessageHandler<ServiceAccountTokenHandler>();
}

// Configure HTTP client for CompiledLayoutService
CompiledLayoutServiceSettings compiledLayoutSettings = builder.Configuration
    .GetSection("CompiledLayoutService")
    .Get<CompiledLayoutServiceSettings>() ?? new CompiledLayoutServiceSettings();

builder.Services.Configure<CompiledLayoutServiceSettings>(builder.Configuration.GetSection("CompiledLayoutService"));

builder.Services.AddHttpClient<ICompiledLayoutRepository, HttpCompiledLayoutRepository>(client =>
{
    if (!string.IsNullOrEmpty(compiledLayoutSettings.BaseUrl))
    {
        client.BaseAddress = new Uri(compiledLayoutSettings.BaseUrl);
    }
});

// Configure HTTP client for LayoutCompilerService
LayoutCompilerServiceSettings layoutCompilerSettings = builder.Configuration
    .GetSection("LayoutCompilerService")
    .Get<LayoutCompilerServiceSettings>() ?? new LayoutCompilerServiceSettings();

builder.Services.Configure<LayoutCompilerServiceSettings>(builder.Configuration.GetSection("LayoutCompilerService"));

if (!string.IsNullOrEmpty(layoutCompilerSettings.BaseUrl) &&
    Uri.TryCreate(layoutCompilerSettings.BaseUrl, UriKind.Absolute, out Uri? compilerBaseUri))
{
    builder.Services.AddHttpClient<ILayoutCompilerClient, HttpLayoutCompilerClient>(client =>
    {
        client.BaseAddress = compilerBaseUri;
    });
}
else
{
    // No LayoutCompilerService URL configured: fall back to the stub implementation.
    // This is expected in local test environments where the Lambda is not running.
    builder.Services.AddSingleton<ILayoutCompilerClient, StubLayoutCompilerClient>();
}

// Register remaining stub implementations
builder.Services.AddSingleton<ILayoutValidationClient, StubLayoutValidationClient>();
builder.Services.AddSingleton<INotificationPublisher, StubNotificationPublisher>();

// Register the orchestration background service.
// Set Orchestrator:Enabled=false to skip registration (e.g. health-check-only E2E tests).
bool orchestratorEnabled = builder.Configuration.GetValue("Orchestrator:Enabled", defaultValue: true);
if (orchestratorEnabled)
{
    builder.Services.AddHostedService<LayoutProcessingOrchestrator>();
}

// Configure JWT Bearer authentication with AWS Cognito
CognitoSettings cognitoSettings = builder.Configuration
    .GetSection("Cognito")
    .Get<CognitoSettings>() ?? new CognitoSettings();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = cognitoSettings.Authority;

        if (!string.IsNullOrEmpty(cognitoSettings.Audience))
        {
            options.Audience = cognitoSettings.Audience;
        }
        else
        {
            // When no audience is configured, skip audience validation.
            options.TokenValidationParameters.ValidateAudience = false;
        }

        // Preserve original claim names from the JWT (don't remap to .NET claim types).
        options.MapInboundClaims = false;

        // Allow HTTP metadata endpoints in non-production environments (local dev and tests).
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
    });

builder.Services.AddAuthorization();
builder.Services.AddOpenApi();

WebApplication app = builder.Build();

ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.ServiceStarting("LayoutProcessingService");

if (app.Environment.IsDevelopment())
{
    await EnsureLocalStackRunningAsync(app, logger).ConfigureAwait(false);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

// Map endpoints
app.MapHealthEndpoints();

// Log the configured listen address; fall back to Kestrel's default.
string listenAddress = app.Configuration["ASPNETCORE_URLS"]
    ?? app.Configuration["urls"]
    ?? "http://localhost:5000";
logger.ServiceStarted("LayoutProcessingService", listenAddress);

app.Run();

static async Task EnsureLocalStackRunningAsync(WebApplication app, ILogger logger)
{
    const int LocalStackHealthCheckTimeoutSeconds = 5;
    const int LocalStackStartupWaitTimeoutSeconds = 30;
    const int LocalStackRetryDelaySeconds = 2;
    TimeSpan localStackStartupWaitTimeout = TimeSpan.FromSeconds(LocalStackStartupWaitTimeoutSeconds);
    TimeSpan localStackRetryDelay = TimeSpan.FromSeconds(LocalStackRetryDelaySeconds);
    string[] requiredServices = ["sqs"];

    string baseUrl = app.Configuration["LocalStack:BaseUrl"] ?? "http://localhost:4566";

    if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri))
    {
        logger.LocalStackDependencyUnavailable(baseUrl, "configuration value is not a valid absolute URL", exception: null);
        Environment.Exit(1);
    }

    Uri healthUri = new(baseUri, "/_localstack/health");

    using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(LocalStackHealthCheckTimeoutSeconds) };
    Exception? lastException = null;
    string? lastFailureReason = null;
    DateTime deadlineUtc = DateTime.UtcNow.Add(localStackStartupWaitTimeout);

    while (DateTime.UtcNow < deadlineUtc)
    {
        try
        {
            using HttpResponseMessage response = await client.GetAsync(healthUri).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                lastFailureReason = $"HTTP {(int)response.StatusCode}";
            }
            else
            {
                using JsonDocument json = JsonDocument.Parse(body);
                if (IsLocalStackRunning(json.RootElement, requiredServices, out string failureReason))
                {
                    return;
                }

                lastFailureReason = failureReason;
            }

            lastException = null;
        }
        catch (Exception ex)
        {
            lastException = ex;
            lastFailureReason = ex.Message;
        }

        await Task.Delay(localStackRetryDelay).ConfigureAwait(false);
    }

    logger.LocalStackDependencyUnavailable(
        healthUri.ToString(),
        $"did not become healthy within {LocalStackStartupWaitTimeoutSeconds}s; last check result: {lastFailureReason ?? "unknown health check failure"}",
        lastException);
    Environment.Exit(1);
}

static bool IsLocalStackRunning(JsonElement root, IReadOnlyList<string> requiredServices, out string failureReason)
{
    if (root.TryGetProperty("status", out JsonElement statusElement))
    {
        string status = statusElement.GetString() ?? string.Empty;
        if (string.Equals(status, "running", StringComparison.OrdinalIgnoreCase))
        {
            failureReason = string.Empty;
            return true;
        }

        failureReason = $"status='{status}'";
        return false;
    }

    if (!root.TryGetProperty("services", out JsonElement servicesElement) || servicesElement.ValueKind != JsonValueKind.Object)
    {
        failureReason = "health response did not contain a running status or services object";
        return false;
    }

    foreach (string service in requiredServices)
    {
        if (!servicesElement.TryGetProperty(service, out JsonElement serviceStatusElement))
        {
            failureReason = $"service '{service}' was missing from health response";
            return false;
        }

        string serviceStatus = serviceStatusElement.GetString() ?? string.Empty;
        if (!string.Equals(serviceStatus, "available", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(serviceStatus, "running", StringComparison.OrdinalIgnoreCase))
        {
            failureReason = $"service '{service}' status was '{serviceStatus}'";
            return false;
        }
    }

    failureReason = string.Empty;
    return true;
}

// Make Program visible for testing
public partial class Program { }
