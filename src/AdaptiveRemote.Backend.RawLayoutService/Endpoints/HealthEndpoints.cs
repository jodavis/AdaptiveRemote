using AdaptiveRemote.Backend.RawLayoutService.Logging;
using AdaptiveRemote.Contracts;
using System.Reflection;

namespace AdaptiveRemote.Backend.RawLayoutService.Endpoints;

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
            ServiceName: "RawLayoutService",
            Version: Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
            Status: "Healthy"
        );

        logger.HealthCheckSuccessful();

        return Results.Json(
            response,
            LayoutContractsJsonContext.Default.HealthResponse);
    }
}
