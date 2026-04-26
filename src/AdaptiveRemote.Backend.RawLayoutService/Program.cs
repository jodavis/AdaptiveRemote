using AdaptiveRemote.Backend.RawLayoutService.Configuration;
using AdaptiveRemote.Backend.RawLayoutService.Endpoints;
using AdaptiveRemote.Backend.RawLayoutService.Logging;
using AdaptiveRemote.Backend.RawLayoutService.Repositories;
using AdaptiveRemote.Backend.RawLayoutService.Services;
using AdaptiveRemote.Contracts;
using Amazon.DynamoDBv2;
using Amazon.SQS;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;
using System.Net.Http;
using System.Text.Json;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure DynamoDB
DynamoDbSettings dynamoDbSettings = builder.Configuration
    .GetSection("DynamoDB")
    .Get<DynamoDbSettings>() ?? new DynamoDbSettings();

builder.Services.Configure<DynamoDbSettings>(builder.Configuration.GetSection("DynamoDB"));

// Create DynamoDB client
IAmazonDynamoDB dynamoDbClient;

if (!string.IsNullOrEmpty(dynamoDbSettings.ServiceUrl))
{
    // LocalStack or custom endpoint - use explicit credentials from environment
    AmazonDynamoDBConfig dynamoDbConfig = new()
    {
        ServiceURL = dynamoDbSettings.ServiceUrl,
        // Don't set RegionEndpoint when using ServiceURL - it overrides the custom endpoint
        AuthenticationRegion = dynamoDbSettings.Region
    };

    string? accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
    string? secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

    if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
    {
        // Use explicit credentials for LocalStack
        dynamoDbClient = new AmazonDynamoDBClient(
            new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey),
            dynamoDbConfig);
    }
    else
    {
        // Fall back to default credential chain
        dynamoDbClient = new AmazonDynamoDBClient(dynamoDbConfig);
    }
}
else
{
    // Production AWS - use default credential chain (IAM roles, etc.)
    AmazonDynamoDBConfig dynamoDbConfig = new()
    {
        RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(dynamoDbSettings.Region)
    };
    dynamoDbClient = new AmazonDynamoDBClient(dynamoDbConfig);
}

builder.Services.AddSingleton(dynamoDbClient);

// Configure SQS for the layout processing trigger
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

    string? sqsAccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
    string? sqsSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

    if (!string.IsNullOrEmpty(sqsAccessKey) && !string.IsNullOrEmpty(sqsSecretKey))
    {
        sqsClient = new AmazonSQSClient(
            new Amazon.Runtime.BasicAWSCredentials(sqsAccessKey, sqsSecretKey),
            sqsConfig);
    }
    else
    {
        sqsClient = new AmazonSQSClient(sqsConfig);
    }
}
else
{
    // Production AWS — use default credential chain
    AmazonSQSConfig sqsConfig = new()
    {
        RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(sqsSettings.Region)
    };
    sqsClient = new AmazonSQSClient(sqsConfig);
}

builder.Services.AddSingleton(sqsClient);

// Register repositories and services
builder.Services.AddSingleton<DynamoDbRawLayoutRepository>();
builder.Services.AddSingleton<IRawLayoutRepository>(sp => sp.GetRequiredService<DynamoDbRawLayoutRepository>());
builder.Services.AddSingleton<IRawLayoutStatusWriter>(sp => sp.GetRequiredService<DynamoDbRawLayoutRepository>());

// Register the layout processing trigger: use SQS if configured, otherwise fall back to no-op stub.
// SQS wiring requires a QueueUrl; environments without SQS (e.g. integration tests without LocalStack)
// continue using the stub so CRUD endpoints remain functional.
if (!string.IsNullOrEmpty(sqsSettings.QueueUrl))
{
    builder.Services.AddSingleton<ILayoutProcessingTrigger, SqsLayoutProcessingTrigger>();
}
else
{
    builder.Services.AddSingleton<ILayoutProcessingTrigger, StubLayoutProcessingTrigger>();
}

// Register stub notification publisher (to be replaced in Task 9)
builder.Services.AddSingleton<INotificationPublisher, StubNotificationPublisher>();

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

// Register the source-generated JSON context so minimal-API model binding can
// deserialize request bodies (e.g. RawLayout on POST/PUT) without reflection.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, LayoutContractsJsonContext.Default);
});

WebApplication app = builder.Build();

ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.ServiceStarting();

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
app.MapLayoutEndpoints();

// Log the configured listen address; fall back to Kestrel's default.
// ASPNETCORE_URLS is the standard env-var; "urls" is the equivalent command-line key.
string listenAddress = app.Configuration["ASPNETCORE_URLS"]
    ?? app.Configuration["urls"]
    ?? "http://localhost:5000";
logger.ServiceStarted(listenAddress);

app.Run();

static async Task EnsureLocalStackRunningAsync(WebApplication app, ILogger logger)
{
    const int LocalStackHealthCheckTimeoutSeconds = 5;
    const int LocalStackStartupWaitTimeoutSeconds = 30;
    const int LocalStackRetryDelaySeconds = 2;
    TimeSpan localStackStartupWaitTimeout = TimeSpan.FromSeconds(LocalStackStartupWaitTimeoutSeconds);
    TimeSpan localStackRetryDelay = TimeSpan.FromSeconds(LocalStackRetryDelaySeconds);
    string[] requiredServices = ["dynamodb"];

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
