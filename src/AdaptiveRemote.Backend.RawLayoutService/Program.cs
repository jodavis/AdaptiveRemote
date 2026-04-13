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
AmazonDynamoDBConfig dynamoDbConfig = new()
{
    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(dynamoDbSettings.Region)
};

if (!string.IsNullOrEmpty(dynamoDbSettings.ServiceUrl))
{
    dynamoDbConfig.ServiceURL = dynamoDbSettings.ServiceUrl;
}

builder.Services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient(dynamoDbConfig));

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
