using AdaptiveRemote.Backend.LayoutProcessingService.Logging;
using AdaptiveRemote.Contracts;
using System.Reflection;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Endpoints;

/// <summary>
/// Health check endpoint. Returns service name, version, and status.
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", GetHealth)
            .WithName(nameof(GetHealth))
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .AllowAnonymous();
    }

    private static IResult GetHealth(ILogger<Program> logger)
    {
        logger.HealthCheckRequested();

        HealthResponse response = new(
            ServiceName: "LayoutProcessingService",
            Version: Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
            Status: "Healthy"
        );

        logger.HealthCheckSuccessful();

        return Results.Json(
            response,
            LayoutContractsJsonContext.Default.HealthResponse);
    }
}
