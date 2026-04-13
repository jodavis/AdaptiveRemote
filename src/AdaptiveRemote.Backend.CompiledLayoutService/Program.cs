using AdaptiveRemote.Backend.CompiledLayoutService.Configuration;
using AdaptiveRemote.Backend.CompiledLayoutService.Endpoints;
using AdaptiveRemote.Backend.CompiledLayoutService.Logging;
using AdaptiveRemote.Backend.CompiledLayoutService.Services;
using AdaptiveRemote.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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
