using AdaptiveRemote.Backend.CompiledLayoutService.Configuration;
using AdaptiveRemote.Backend.CompiledLayoutService.Endpoints;
using AdaptiveRemote.Backend.CompiledLayoutService.Logging;
using AdaptiveRemote.Backend.CompiledLayoutService.Services;
using AdaptiveRemote.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;
using System.Net.Http;
using System.Text.Json;

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
builder.Services.AddOpenApi();

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
    string[] requiredServices = ["dynamodb", "lambda", "sqs"];

    string baseUrl = app.Configuration["LocalStack:BaseUrl"] ?? "http://localhost:4566";

    if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri))
    {
        logger.LocalStackDependencyUnavailable(baseUrl, "configuration value is not a valid absolute URL");
        Environment.Exit(1);
    }

    Uri healthUri = new(baseUri, "/_localstack/health");

    using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(LocalStackHealthCheckTimeoutSeconds) };

    try
    {
        using HttpResponseMessage response = await client.GetAsync(healthUri).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            logger.LocalStackDependencyUnavailable(healthUri.ToString(), $"HTTP {(int)response.StatusCode}");
            Environment.Exit(1);
        }

        using JsonDocument json = JsonDocument.Parse(body);
        if (!IsLocalStackRunning(json.RootElement, requiredServices, out string failureReason))
        {
            logger.LocalStackDependencyUnavailable(healthUri.ToString(), failureReason);
            Environment.Exit(1);
        }
    }
    catch (Exception ex)
    {
        logger.LocalStackDependencyUnavailable(healthUri.ToString(), ex.Message);
        Environment.Exit(1);
    }
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
