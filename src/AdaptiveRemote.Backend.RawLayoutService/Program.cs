using AdaptiveRemote.Backend.RawLayoutService.Configuration;
using AdaptiveRemote.Backend.RawLayoutService.Endpoints;
using AdaptiveRemote.Backend.RawLayoutService.Logging;
using AdaptiveRemote.Backend.RawLayoutService.Repositories;
using AdaptiveRemote.Backend.RawLayoutService.Services;
using AdaptiveRemote.Contracts;
using Amazon.DynamoDBv2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

// Register repositories and services
builder.Services.AddSingleton<DynamoDbRawLayoutRepository>();
builder.Services.AddSingleton<IRawLayoutRepository>(sp => sp.GetRequiredService<DynamoDbRawLayoutRepository>());
builder.Services.AddSingleton<IRawLayoutStatusWriter>(sp => sp.GetRequiredService<DynamoDbRawLayoutRepository>());

// Register stub implementations (to be replaced in later tasks)
builder.Services.AddSingleton<ILayoutProcessingTrigger, StubLayoutProcessingTrigger>();
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

WebApplication app = builder.Build();

ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.ServiceStarting();

app.UseAuthentication();
app.UseAuthorization();

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

// Make Program visible for testing
public partial class Program { }
