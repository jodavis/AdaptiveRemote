using AdaptiveRemote.Backend.Common.Logging;
using AdaptiveRemote.Backend.CompiledLayoutService.Configuration;
using AdaptiveRemote.Backend.CompiledLayoutService.Endpoints;
using AdaptiveRemote.Backend.CompiledLayoutService.Services;
using AdaptiveRemote.Contracts;
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

// Register services
builder.Services.AddSingleton<ICompiledLayoutRepository, HardcodedLayoutProvider>();

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
logger.ServiceStarting("CompiledLayoutService");

// CompiledLayoutService currently uses HardcodedLayoutProvider and doesn't require LocalStack.
// The LocalStack check is removed so API tests can run without Docker.
// When CompiledLayoutService gains a real DynamoDB implementation, restore the conditional
// check similar to RawLayoutService (only when ServiceUrl is configured).

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
logger.ServiceStarted("CompiledLayoutService", listenAddress);

app.Run();

// Make Program visible for testing
public partial class Program { }
